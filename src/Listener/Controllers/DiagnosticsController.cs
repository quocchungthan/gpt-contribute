using Listener.Data;
using Listener.Models;
using Listener.Operations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Listener.Controllers;

public sealed class DiagnosticsController(IDbContextFactory<ListenerDbContext> dbFactory, AppMonitor monitor) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var events = await db.OperationalEvents.AsNoTracking().OrderByDescending(x => x.OccurredAt).Take(50).ToListAsync(ct);
        return View(new DiagnosticsViewModel(monitor.Snapshot(), events));
    }

    [HttpGet("monitoring/v1/capture")]
    public IActionResult Capture() => Ok(new { schemaVersion = 1, feature = "capture", monitor = monitor.Snapshot() });
}
