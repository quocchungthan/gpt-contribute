using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Listener.Data;
using Listener.Operations;
using Listener.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Listener.Recordings;

public sealed record DeleteResult(bool Succeeded, IReadOnlyList<Guid> ProtectedIds, IReadOnlyList<Guid> FailedIds, string OperationId);

public sealed class RecordingService(IDbContextFactory<ListenerDbContext> dbFactory, RecordingFiles files, OperationLog events, IOptions<ListenerOptions> options)
{
    public async Task<List<Recording>> ListAsync(string sort = "newest", CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Recordings.AsNoTracking().Where(x => x.State == RecordingState.Complete).Include(x => x.Tags).ToListAsync(ct);
        return (sort switch
        {
            "oldest" => rows.OrderBy(x => x.StartedAt), "size" => rows.OrderByDescending(x => x.ByteCount),
            "duration" => rows.OrderByDescending(x => x.Duration), "average" => rows.OrderByDescending(x => x.AverageRms),
            "peak" => rows.OrderByDescending(x => x.PeakLevel), "speech" => rows.OrderByDescending(x => x.SpeechRatio),
            _ => rows.OrderByDescending(x => x.StartedAt)
        }).ToList();
    }

    public async Task<Recording?> FindAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await db.Recordings.AsNoTracking().Include(x => x.Tags).SingleOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task SetProtectionAsync(Guid id, bool value, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var row = await db.Recordings.SingleAsync(x => x.Id == id, ct);
        row.IsProtected = value; row.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<DeleteResult> DeleteAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        var operationId = Guid.NewGuid().ToString("N");
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Recordings.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        var protectedIds = rows.Where(x => x.IsProtected).Select(x => x.Id).ToList();
        if (protectedIds.Count > 0)
        {
            await events.WriteAsync("recording.delete_blocked", operationId, "Deletion was blocked because the selection contains protected recordings.", "Warning", ct);
            return new(false, protectedIds, [], operationId);
        }
        var failed = new List<Guid>();
        foreach (var row in rows)
        {
            try
            {
                row.State = RecordingState.Deleting; await db.SaveChangesAsync(ct);
                var path = files.FullPath(row.RelativeFileName); if (File.Exists(path)) File.Delete(path);
                db.Recordings.Remove(row); await db.SaveChangesAsync(ct);
            }
            catch { failed.Add(row.Id); }
        }
        await events.WriteAsync(failed.Count == 0 ? "recording.delete_completed" : "recording.delete_failed", operationId, failed.Count == 0 ? $"Deleted {rows.Count} recordings." : $"Could not delete {failed.Count} recordings.", failed.Count == 0 ? "Information" : "Error", ct);
        return new(failed.Count == 0, [], failed, operationId);
    }

    public async Task<string> ExportAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        var operationId = Guid.NewGuid().ToString("N");
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var rows = await db.Recordings.AsNoTracking().Where(x => ids.Contains(x.Id) && x.State == RecordingState.Complete).ToListAsync(ct);
        var exportRoot = Path.Combine(files.Root, "exports"); Directory.CreateDirectory(exportRoot);
        var zipPath = Path.Combine(exportRoot, $"listener-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
        await using var output = File.Create(zipPath); using var archive = new ZipArchive(output, ZipArchiveMode.Create);
        var manifest = new List<object>();
        foreach (var row in rows)
        {
            var path = files.FullPath(row.RelativeFileName); if (!File.Exists(path)) continue;
            var name = $"recordings/{row.Id:N}.wav"; archive.CreateEntryFromFile(path, name);
            await using var stream = File.OpenRead(path); var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct));
            manifest.Add(new { row.Id, row.StartedAt, row.EndedAt, row.Title, row.Notes, File = name, Sha256 = hash });
        }
        var manifestEntry = archive.CreateEntry("manifest.json"); await using (var target = manifestEntry.Open()) await JsonSerializer.SerializeAsync(target, manifest, cancellationToken: ct);
        await events.WriteAsync("recording.export_completed", operationId, $"Exported {rows.Count} recordings.", ct: ct);
        return zipPath;
    }

    public async Task<int> ApplyRetentionAsync(CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-options.Value.Retention.MaximumAgeDays);
        var candidates = await db.Recordings.Where(x => x.State == RecordingState.Complete && !x.IsProtected).Select(x => new { x.Id, x.EndedAt }).ToListAsync(ct);
        var ids = candidates.Where(x => x.EndedAt < cutoff).Select(x => x.Id).ToList();
        if (ids.Count > 0) await DeleteAsync(ids, ct);
        return ids.Count;
    }
}
