using Listener.Data;

namespace Listener.Operations;

public sealed class AppMonitor
{
    private readonly object _gate = new();
    private CaptureStatus _status = CaptureStatus.Paused;
    private DateTimeOffset _observedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset? _lastSuccessAt;
    private string? _lastSuccess;
    private string? _lastFailure;
    private string? _operationId;
    private long _frames;
    private int _activeChunks;

    public void Update(CaptureStatus status, string? operationId = null, string? success = null, string? failure = null)
    {
        lock (_gate)
        {
            _status = status; _observedAt = DateTimeOffset.UtcNow; _operationId = operationId ?? _operationId;
            if (success is not null) { _lastSuccess = success; _lastSuccessAt = _observedAt; _lastFailure = null; }
            if (failure is not null) _lastFailure = failure;
        }
    }

    public void FrameReceived() { lock (_gate) _frames++; }
    public void ActiveChunks(int value) { lock (_gate) _activeChunks = value; }

    public MonitorSnapshot Snapshot()
    {
        lock (_gate) return new(_status.ToString(), _observedAt, DateTimeOffset.UtcNow - _observedAt > TimeSpan.FromSeconds(10), _operationId, _lastSuccess, _lastSuccessAt, _lastFailure, _frames, _activeChunks);
    }
}

public sealed record MonitorSnapshot(string State, DateTimeOffset ObservedAtUtc, bool IsStale, string? OperationId, string? LastSuccess, DateTimeOffset? LastSuccessAtUtc, string? LastFailure, long FramesReceived, int ActiveChunks);
