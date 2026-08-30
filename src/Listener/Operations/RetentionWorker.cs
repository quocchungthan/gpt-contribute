using Listener.Recordings;

namespace Listener.Operations;

public sealed class RetentionWorker(RecordingService recordings, ILogger<RetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await recordings.ApplyRetentionAsync(stoppingToken); }
                catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "retention.failed"); }
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
