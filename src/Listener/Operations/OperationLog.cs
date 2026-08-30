using Listener.Data;
using Microsoft.EntityFrameworkCore;

namespace Listener.Operations;

public sealed class OperationLog(IDbContextFactory<ListenerDbContext> dbFactory, ILogger<OperationLog> logger)
{
    public async Task WriteAsync(string eventName, string operationId, string summary, string level = "Information", CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        db.OperationalEvents.Add(new OperationalEvent { EventName = eventName, OperationId = operationId, Summary = summary, Level = level });
        await db.SaveChangesAsync(ct);
        logger.Log(level == "Error" ? LogLevel.Error : level == "Warning" ? LogLevel.Warning : LogLevel.Information,
            "{EventName} OperationId={OperationId} Summary={Summary}", eventName, operationId, summary);
    }
}
