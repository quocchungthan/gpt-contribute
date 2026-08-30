using Listener.Data;
using Listener.Operations;
using Listener.Recordings;
using Listener.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NAudio.Wave;

namespace Listener.Capture;

public sealed record AudioDevice(string Id, string Name);

public sealed class CaptureManager : IDisposable
{
    private readonly object _gate = new();
    private readonly IDbContextFactory<ListenerDbContext> _dbFactory;
    private readonly RecordingFiles _files;
    private readonly AppMonitor _monitor;
    private readonly OperationLog _events;
    private readonly ListenerOptions _options;
    private readonly ILogger<CaptureManager> _logger;
    private WaveInEvent? _input;
    private WaveFileWriter? _writer;
    private readonly Queue<byte[]> _preRoll = new();
    private string? _temporaryPath;
    private Guid? _recordingId;
    private DateTimeOffset _chunkStarted;
    private int _silentFrames;
    private long _samples;
    private double _sumSquares;
    private double _peak;
    private long _speechFrames;
    private long _totalFrames;
    private bool _testMode;
    private CancellationTokenSource? _testStop;

    public CaptureManager(IDbContextFactory<ListenerDbContext> dbFactory, RecordingFiles files, AppMonitor monitor, OperationLog events, IOptions<ListenerOptions> options, ILogger<CaptureManager> logger)
    {
        _dbFactory = dbFactory; _files = files; _monitor = monitor; _events = events; _options = options.Value; _logger = logger;
    }

    public IReadOnlyList<AudioDevice> Devices()
    {
        if (!OperatingSystem.IsWindows()) return [];
        return Enumerable.Range(0, WaveInEvent.DeviceCount).Select(i => new AudioDevice(i.ToString(), WaveInEvent.GetCapabilities(i).ProductName)).ToList();
    }

    public async Task<string> StartAsync(string deviceId, bool testMode = false, CancellationToken ct = default)
    {
        var operationId = Guid.NewGuid().ToString("N");
        if (!OperatingSystem.IsWindows())
        {
            _monitor.Update(CaptureStatus.Faulted, operationId, failure: "Audio capture is available only on Windows.");
            await _events.WriteAsync("capture.platform_unsupported", operationId, "Windows microphone capture is unavailable on this operating system.", "Error", ct);
            return operationId;
        }

        lock (_gate)
        {
            if (_input is not null) return operationId;
            if (!int.TryParse(deviceId, out var deviceNumber) || deviceNumber < 0 || deviceNumber >= WaveInEvent.DeviceCount)
                throw new InvalidOperationException("The selected microphone is unavailable.");

            _testMode = testMode;
            _monitor.Update(CaptureStatus.Starting, operationId);
            _input = new WaveInEvent
            {
                DeviceNumber = deviceNumber,
                WaveFormat = new WaveFormat(16_000, 16, 1),
                BufferMilliseconds = _options.Capture.FrameMilliseconds,
                NumberOfBuffers = 3
            };
            _input.DataAvailable += OnDataAvailable;
            _input.RecordingStopped += OnRecordingStopped;
            _input.StartRecording();
            _monitor.Update(CaptureStatus.Listening, operationId, testMode ? "Microphone test started." : "Listening started.");
        }
        await _events.WriteAsync(testMode ? "capture.test_started" : "capture.started", operationId, testMode ? "Microphone test started." : "Listening started.", ct: ct);
        return operationId;
    }

    public async Task<string> StartTestAsync(string deviceId, CancellationToken ct = default)
    {
        var id = await StartAsync(deviceId, true, ct);
        _testStop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(Timeout.Infinite, _testStop.Token); }
            catch (OperationCanceledException) { await PauseAsync(); }
        });
        return id;
    }

    public async Task<string> PauseAsync(CancellationToken ct = default)
    {
        var operationId = Guid.NewGuid().ToString("N");
        WaveInEvent? input;
        lock (_gate) { input = _input; _input = null; }
        input?.StopRecording();
        await FinalizeChunkAsync(operationId, ct);
        _monitor.Update(CaptureStatus.Paused, operationId, "Listening paused.");
        await _events.WriteAsync("capture.paused", operationId, "Listening paused.", ct: ct);
        return operationId;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_gate)
        {
            _monitor.FrameReceived();
            var frame = e.Buffer.AsSpan(0, e.BytesRecorded).ToArray();
            var (rms, peak, samples) = Measure(frame);
            var speech = rms >= _options.Capture.VoiceThresholdRms;
            var maxPreFrames = Math.Max(1, _options.Capture.PreRollSeconds * 1000 / _options.Capture.FrameMilliseconds);
            _preRoll.Enqueue(frame);
            while (_preRoll.Count > maxPreFrames) _preRoll.Dequeue();

            var beganChunk = false;
            if (_writer is null && (speech || _testMode)) { BeginChunk(sender as WaveInEvent, _preRoll); beganChunk = _writer is not null; }
            if (_writer is null) return;

            if (!beganChunk) _writer.Write(frame, 0, frame.Length);
            _samples += samples; _sumSquares += rms * rms * samples; _peak = Math.Max(_peak, peak); _totalFrames++;
            if (speech) { _speechFrames++; _silentFrames = 0; } else _silentFrames++;

            var silenceLimit = (_options.Capture.SilenceGapSeconds + _options.Capture.PostRollSeconds) * 1000 / _options.Capture.FrameMilliseconds;
            var tooLong = DateTimeOffset.UtcNow - _chunkStarted >= TimeSpan.FromMinutes(_options.Capture.MaximumChunkMinutes);
            if (!_testMode && (_silentFrames >= silenceLimit || tooLong)) _ = FinalizeChunkAsync(Guid.NewGuid().ToString("N"));
        }
    }

    private void BeginChunk(WaveInEvent? input, IEnumerable<byte[]> preRoll)
    {
        if (input is null || _files.UsedBytes() >= _options.Storage.MaximumBytes)
        {
            _monitor.Update(CaptureStatus.StorageFull, failure: "Storage is full. New recordings are not being saved.");
            return;
        }
        _files.EnsureCreated();
        _recordingId = Guid.NewGuid();
        _temporaryPath = _files.FullPath($"{_recordingId:N}.wav.tmp");
        _writer = new WaveFileWriter(_temporaryPath, input.WaveFormat);
        foreach (var buffered in preRoll) _writer.Write(buffered, 0, buffered.Length);
        _chunkStarted = DateTimeOffset.UtcNow;
        _silentFrames = 0; _samples = 0; _sumSquares = 0; _peak = 0; _speechFrames = 0; _totalFrames = 0;
        _monitor.ActiveChunks(1);
        _monitor.Update(CaptureStatus.Listening, success: "Storage is available. Saving is active.");
    }

    private async Task FinalizeChunkAsync(string operationId, CancellationToken ct = default)
    {
        WaveFileWriter? writer; string? temp; Guid? id; DateTimeOffset started; long samples; double sumSquares; double peak; long speechFrames; long totalFrames;
        lock (_gate)
        {
            writer = _writer; temp = _temporaryPath; id = _recordingId; started = _chunkStarted; samples = _samples; sumSquares = _sumSquares; peak = _peak; speechFrames = _speechFrames; totalFrames = _totalFrames;
            _writer = null; _temporaryPath = null; _recordingId = null; _preRoll.Clear(); _monitor.ActiveChunks(0);
        }
        if (writer is null || temp is null || id is null) return;
        writer.Dispose();
        var finalName = $"{id:N}.wav";
        var finalPath = _files.FullPath(finalName);
        File.Move(temp, finalPath, true);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var preferences = await db.Preferences.FindAsync([1], ct);
        db.Recordings.Add(new Recording
        {
            Id = id.Value, StartedAt = started, EndedAt = DateTimeOffset.UtcNow,
            DeviceId = preferences?.SelectedDeviceId ?? "", DeviceName = preferences?.SelectedDeviceName ?? "Selected microphone",
            State = RecordingState.Complete, RelativeFileName = finalName, ByteCount = new FileInfo(finalPath).Length,
            AverageRms = samples == 0 ? 0 : Math.Sqrt(sumSquares / samples), PeakLevel = peak,
            SpeechRatio = totalFrames == 0 ? 0 : (double)speechFrames / totalFrames
        });
        await db.SaveChangesAsync(ct);
        _monitor.Update(CaptureStatus.Listening, operationId, "A recording was saved.");
        await _events.WriteAsync("capture.chunk_completed", operationId, "A recording was saved.", ct: ct);
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (sender is IDisposable disposable) disposable.Dispose();
        if (e.Exception is not null)
        {
            _logger.LogError(e.Exception, "capture.failed");
            _monitor.Update(CaptureStatus.Faulted, failure: "The microphone stopped unexpectedly.");
        }
    }

    private static (double Rms, double Peak, int Samples) Measure(byte[] frame)
    {
        double squares = 0, peak = 0; var count = frame.Length / 2;
        for (var i = 0; i + 1 < frame.Length; i += 2)
        {
            var value = Math.Abs(BitConverter.ToInt16(frame, i) / 32768d);
            squares += value * value; peak = Math.Max(peak, value);
        }
        return (count == 0 ? 0 : Math.Sqrt(squares / count), peak, count);
    }

    public void Dispose() { _input?.Dispose(); _writer?.Dispose(); _testStop?.Dispose(); }
}
