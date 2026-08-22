using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Api.DatabaseBackup.Host.Services;

public sealed class DatabaseBackupOutboxPublisher(
    IDatabaseBackupExecutionJournal journal,
    IDatabaseBackupServiceEventTransport transport,
    DatabaseBackupHostOptions options,
    ILogger<DatabaseBackupOutboxPublisher> logger) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await transport.StartAsync(cancellationToken).ConfigureAwait(false);
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<int> PublishPendingOnceAsync(CancellationToken cancellationToken)
    {
        var count = 0;
        await foreach (var pending in journal.ReadPendingServiceEventsAsync(options.OutboxBatchSize, cancellationToken).ConfigureAwait(false))
        {
            await transport.PublishAsync(pending.Event, cancellationToken).ConfigureAwait(false);
            await journal.MarkServiceEventPublishedAsync(
                pending.EventId, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            count++;
        }
        return count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var published = await PublishPendingOnceAsync(stoppingToken).ConfigureAwait(false);
                if (published == 0)
                    await Task.Delay(options.PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "DatabaseBackup service-event outbox publish failed; the event remains pending.");
                await Task.Delay(options.PollInterval, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        await transport.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
