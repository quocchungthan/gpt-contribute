using Listener.Capture;

namespace Listener.Tests;

public sealed class CaptureFailureClassifierTests
{
    [Fact]
    public void PermissionFailureRequiresUserActionAndNoBlindRetry()
    {
        var result = CaptureFailureClassifier.Classify(new UnauthorizedAccessException());

        Assert.Equal(CaptureFailureKind.PermissionDenied, result.Kind);
        Assert.Equal("DEVICE_PERMISSION_DENIED", result.Code);
        Assert.True(result.RequiresUserAction);
        Assert.False(result.MayRetryAutomatically);
        Assert.Contains("Windows Settings", result.NextAction);
    }

    [Fact]
    public void MissingDeviceExplainsThatSelectionMustChangeOrReconnect()
    {
        var result = CaptureFailureClassifier.Missing();

        Assert.Equal(CaptureFailureKind.DeviceMissing, result.Kind);
        Assert.True(result.RequiresUserAction);
        Assert.Contains("Kết nối lại", result.NextAction);
    }

    [Fact]
    public void MonitorCarriesUserActionAndSafeDiagnosticCode()
    {
        var monitor = new Listener.Operations.AppMonitor();
        monitor.Update(Listener.Data.CaptureStatus.Degraded, "op", failure: "Micrô bị ngắt.", errorCode: "DEVICE_DISCONNECTED", nextAction: "Kết nối lại micrô.", requiresUserAction: true);

        var snapshot = monitor.Snapshot();

        Assert.True(snapshot.RequiresUserAction);
        Assert.Equal("DEVICE_DISCONNECTED", snapshot.ErrorCode);
        Assert.Equal("Kết nối lại micrô.", snapshot.NextAction);
    }
}
