namespace Listener.Capture;

public enum CaptureFailureKind { DeviceMissing, DeviceDisconnected, PermissionDenied, DeviceBusy, Storage, TransientSoftware, Unknown }

public sealed record CaptureFailure(CaptureFailureKind Kind, string Code, string UserMessage, string NextAction, bool RequiresUserAction, bool MayRetryAutomatically);

public static class CaptureFailureClassifier
{
    private const int AccessDenied = unchecked((int)0x80070005);
    private const int DeviceNotConnected = unchecked((int)0x8007048F);
    private const int DeviceInUse = unchecked((int)0x8889000A);

    public static CaptureFailure Classify(Exception exception)
    {
        var root = exception.GetBaseException();
        return root switch
        {
            UnauthorizedAccessException => Permission(),
            IOException io when io.HResult == DeviceNotConnected => Disconnected(),
            _ when root.HResult == AccessDenied => Permission(),
            _ when root.HResult == DeviceNotConnected => Disconnected(),
            _ when root.HResult == DeviceInUse => Busy(),
            _ => new(CaptureFailureKind.Unknown, "CAPTURE_UNKNOWN", "Không thể sử dụng micrô.", "Mở Chẩn đoán, kiểm tra micrô đã chọn rồi thử lại.", true, false)
        };
    }

    public static CaptureFailure Missing() => new(CaptureFailureKind.DeviceMissing, "DEVICE_MISSING", "Không tìm thấy micrô đã chọn.", "Kết nối lại micrô hoặc chọn một thiết bị khác trong Cài đặt.", true, false);
    public static CaptureFailure Disconnected() => new(CaptureFailureKind.DeviceDisconnected, "DEVICE_DISCONNECTED", "Micrô đã bị ngắt kết nối.", "Kết nối lại micrô. Listener sẽ không tự chuyển sang thiết bị khác.", true, false);
    public static CaptureFailure Permission() => new(CaptureFailureKind.PermissionDenied, "DEVICE_PERMISSION_DENIED", "Windows không cho phép Listener dùng micrô.", "Mở Windows Settings → Privacy & security → Microphone và cấp quyền cho ứng dụng máy tính.", true, false);
    public static CaptureFailure Busy() => new(CaptureFailureKind.DeviceBusy, "DEVICE_BUSY", "Micrô đang được ứng dụng khác sử dụng độc quyền.", "Đóng ứng dụng đang giữ micrô hoặc tắt chế độ độc quyền, rồi thử lại.", true, false);
}
