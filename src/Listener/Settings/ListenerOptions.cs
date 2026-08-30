using System.ComponentModel.DataAnnotations;

namespace Listener.Settings;

public sealed class ListenerOptions
{
    [Required, MinLength(1)] public List<SupportedLocale> SupportedLocales { get; init; } = [];
    [Required] public RetentionOptions Retention { get; init; } = new();
    [Required] public StorageOptions Storage { get; init; } = new();
    [Required] public CaptureOptions Capture { get; init; } = new();
}

public sealed class SupportedLocale
{
    [Required] public string CountryCode { get; init; } = "VN";
    [Required] public string LanguageCode { get; init; } = "vi";
    [Required] public string DisplayName { get; init; } = "Vietnam";
    [Required] public string LanguageDisplayName { get; init; } = "Tiếng Việt";
}

public sealed class RetentionOptions
{
    [Range(1, 3650)] public int MaximumAgeDays { get; init; } = 7;
}

public sealed class StorageOptions
{
    [Range(1, long.MaxValue)] public long MaximumBytes { get; init; } = 5L * 1024 * 1024 * 1024;
    [Range(1, 99)] public int WarningThresholdPercent { get; init; } = 80;
}

public sealed class CaptureOptions
{
    [Range(100, 10_000)] public int FrameMilliseconds { get; init; } = 100;
    [Range(0.0001, 1)] public double VoiceThresholdRms { get; init; } = 0.012;
    [Range(0, 30)] public int PreRollSeconds { get; init; } = 3;
    [Range(0, 30)] public int PostRollSeconds { get; init; } = 5;
    [Range(1, 30)] public int SilenceGapSeconds { get; init; } = 2;
    [Range(1, 60)] public int MaximumChunkMinutes { get; init; } = 15;
}
