using Listener.Capture;
using Listener.Data;
using Listener.Models;
using Listener.Operations;
using Listener.Recordings;
using Listener.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Listener.Controllers;

public sealed class HomeController(IDbContextFactory<ListenerDbContext> dbFactory, CaptureManager capture, AppMonitor monitor, RecordingFiles files, IOptions<ListenerOptions> options) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var preference = await db.Preferences.SingleAsync(x => x.Id == 1, ct);
        if (!preference.OnboardingAccepted) return RedirectToAction(nameof(Setup));
        var counts = await db.Recordings.Where(x => x.State == RecordingState.Complete).GroupBy(_ => 1).Select(g => new { All = g.Count(), Protected = g.Count(x => x.IsProtected) }).SingleOrDefaultAsync(ct);
        return View(new DashboardViewModel(preference, monitor.Snapshot(), files.UsedBytes(), options.Value.Storage.MaximumBytes, options.Value.Storage.WarningThresholdPercent, counts?.All ?? 0, counts?.Protected ?? 0));
    }

    public async Task<IActionResult> Setup(string? message, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var p = await db.Preferences.SingleAsync(x => x.Id == 1, ct);
        return View(new SetupViewModel(p.OnboardingAccepted, p.SelectedDeviceId, capture.Devices(), message));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSetup(bool accepted, string deviceId, CancellationToken ct)
    {
        var device = capture.Devices().SingleOrDefault(x => x.Id == deviceId);
        if (!accepted || device is null) return RedirectToAction(nameof(Setup), new { message = "Hãy xác nhận thông báo và chọn micrô hợp lệ." });
        await using var db = await dbFactory.CreateDbContextAsync(ct); var p = await db.Preferences.SingleAsync(x => x.Id == 1, ct);
        p.OnboardingAccepted = true; p.OnboardingAcceptedAt = DateTimeOffset.UtcNow; p.SelectedDeviceId = device.Id; p.SelectedDeviceName = device.Name; await db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(bool test = false, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct); var p = await db.Preferences.SingleAsync(x => x.Id == 1, ct);
        if (p.SelectedDeviceId is null) return RedirectToAction(nameof(Setup));
        if (test) await capture.StartTestAsync(p.SelectedDeviceId, ct); else await capture.StartAsync(p.SelectedDeviceId, false, ct);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Pause(CancellationToken ct) { await capture.PauseAsync(ct); return RedirectToAction(nameof(Index)); }
}
