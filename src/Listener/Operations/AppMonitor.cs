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
    private string? _errorCode;
    private string? _nextAction;
    private bool _requiresUserAction;
    private string? _operationId;
    private long _frames;
    private int _activeChunks;

    public void Update(CaptureStatus status, string? operationId = null, string? success = null, string? failure = null, string? errorCode = null, string? nextAction = null, bool requiresUserAction = false)
    {
        lock (_gate)
        {
            _status = status; _observedAt = DateTimeOffset.UtcNow; _operationId = operationId ?? _operationId;
            if (success is not null) { _lastSuccess = success; _lastSuccessAt = _observedAt; _lastFailure = null; _errorCode = null; _nextAction = null; _requiresUserAction = false; }
            if (failure is not null) { _lastFailure = failure; _errorCode = errorCode; _nextAction = nextAction; _requiresUserAction = requiresUserAction; }
        }
    }

    public void FrameReceived() { lock (_gate) _frames++; }
    public void ActiveChunks(int value) { lock (_gate) _activeChunks = value; }

    public MonitorSnapshot Snapshot()
    {
        lock (_gate) return new(_status.ToString(), _observedAt, DateTimeOffset.UtcNow - _observedAt > TimeSpan.FromSeconds(10), _operationId, _lastSuccess, _lastSuccessAt, _lastFailure, _errorCode, _nextAction, _requiresUserAction, _frames, _activeChunks);
    }
}

public sealed record MonitorSnapshot(string State, DateTimeOffset ObservedAtUtc, bool IsStale, string? OperationId, string? LastSuccess, DateTimeOffset? LastSuccessAtUtc, string? LastFailure, string? ErrorCode, string? NextAction, bool RequiresUserAction, long FramesReceived, int ActiveChunks);
