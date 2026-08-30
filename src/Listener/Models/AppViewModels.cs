using Listener.Data;
using Listener.Operations;

namespace Listener.Models;

public sealed record DashboardViewModel(AppPreference Preferences, MonitorSnapshot Monitor, long UsedBytes, long MaximumBytes, int WarningPercent, int RecordingCount, int ProtectedCount);
public sealed record SetupViewModel(bool Accepted, string? SelectedDeviceId, IReadOnlyList<Listener.Capture.AudioDevice> Devices, string? Message);
public sealed record LibraryViewModel(IReadOnlyList<Recording> Recordings, string Sort, long UsedBytes, long MaximumBytes);
public sealed record DiagnosticsViewModel(MonitorSnapshot Monitor, IReadOnlyList<OperationalEvent> Events);
