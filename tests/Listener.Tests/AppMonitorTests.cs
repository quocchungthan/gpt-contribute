using Listener.Data;
using Listener.Operations;

namespace Listener.Tests;

public sealed class AppMonitorTests
{
    [Fact]
    public void SnapshotReflectsActualStateAndMeasurements()
    {
        var monitor = new AppMonitor();
        monitor.Update(CaptureStatus.Listening, "operation-1", "Listening started.");
        monitor.FrameReceived();
        monitor.ActiveChunks(1);

        var result = monitor.Snapshot();

        Assert.Equal("Listening", result.State);
        Assert.Equal("operation-1", result.OperationId);
        Assert.Equal(1, result.FramesReceived);
        Assert.Equal(1, result.ActiveChunks);
        Assert.False(result.IsStale);
    }
}
