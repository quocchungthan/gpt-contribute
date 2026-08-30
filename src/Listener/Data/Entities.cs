namespace Listener.Data;

public enum RecordingState { Writing, Complete, Quarantined, Deleting }
public enum CaptureStatus { Paused, Starting, Listening, Degraded, Faulted, StorageFull }

public sealed class Recording
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset EndedAt { get; set; }
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public RecordingState State { get; set; } = RecordingState.Writing;
    public string RelativeFileName { get; set; } = "";
    public long ByteCount { get; set; }
    public double AverageRms { get; set; }
    public double PeakLevel { get; set; }
    public double SpeechRatio { get; set; }
    public double? DetectorConfidence { get; set; }
    public string? Title { get; set; }
    public string? Notes { get; set; }
    public bool IsProtected { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<RecordingTag> Tags { get; set; } = [];
    public TimeSpan Duration => EndedAt - StartedAt;
}

public sealed class RecordingTag
{
    public int Id { get; set; }
    public Guid RecordingId { get; set; }
    public Recording Recording { get; set; } = null!;
    public string Value { get; set; } = "";
}

public sealed class AppPreference
{
    public int Id { get; set; } = 1;
    public bool OnboardingAccepted { get; set; }
    public DateTimeOffset? OnboardingAcceptedAt { get; set; }
    public string? SelectedDeviceId { get; set; }
    public string? SelectedDeviceName { get; set; }
    public bool StorageWarningShown { get; set; }
}

public sealed class OperationalEvent
{
    public long Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string EventName { get; set; } = "";
    public string Level { get; set; } = "Information";
    public string OperationId { get; set; } = "";
    public string Summary { get; set; } = "";
}
