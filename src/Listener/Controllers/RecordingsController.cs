using Listener.Models;
using Listener.Recordings;
using Listener.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Listener.Controllers;

public sealed class RecordingsController(RecordingService recordings, RecordingFiles files, IOptions<ListenerOptions> options) : Controller
{
    public async Task<IActionResult> Index(string sort = "newest", CancellationToken ct = default) => View(new LibraryViewModel(await recordings.ListAsync(sort, ct), sort, files.UsedBytes(), options.Value.Storage.MaximumBytes));
    public async Task<IActionResult> Details(Guid id, CancellationToken ct) => (await recordings.FindAsync(id, ct)) is { } row ? View(row) : NotFound();
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> Protect(Guid id, bool value, CancellationToken ct) { await recordings.SetProtectionAsync(id, value, ct); return RedirectToAction(nameof(Details), new { id }); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid[] ids, CancellationToken ct)
    {
        var result = await recordings.DeleteAsync(ids, ct);
        TempData[result.Succeeded ? "Success" : "Error"] = result.ProtectedIds.Count > 0 ? $"Không thể xóa: {result.ProtectedIds.Count} bản ghi đang được bảo vệ." : result.FailedIds.Count > 0 ? $"Không thể xóa {result.FailedIds.Count} bản ghi." : $"Đã xóa {ids.Length} bản ghi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Export(Guid[] ids, CancellationToken ct)
    {
        var path = await recordings.ExportAsync(ids, ct);
        return PhysicalFile(path, "application/zip", Path.GetFileName(path));
    }

    [HttpGet("recordings/{id:guid}/audio")]
    public async Task<IActionResult> Audio(Guid id, CancellationToken ct)
    {
        var row = await recordings.FindAsync(id, ct); if (row is null) return NotFound();
        var path = files.FullPath(row.RelativeFileName); return System.IO.File.Exists(path) ? PhysicalFile(path, "audio/wav", enableRangeProcessing: true) : NotFound();
    }
}
