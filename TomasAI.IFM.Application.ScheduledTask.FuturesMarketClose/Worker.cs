using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Application.Shared.ServiceApi;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Application.ScheduledTask.Shared;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;

namespace TomasAI.IFM.Application.ScheduledTask.FuturesMarketClose;

public sealed class Worker(
    IHostApplicationLifetime lifetime,
    ScheduledTaskOutcome outcome,
    ILogger<Worker> logger,
    IApplicationCommandApi applicationCommandApi,
    INatsJetStreamEndOfDayMaintenance jetStreamMaintenance,
    IDatabaseBackupCommandApi databaseBackupCommandApi,
    IActorProducer actorProducer,
    IConfiguration configuration,
    IMarketDataQueryApi marketQueries,
    IMarketDataFeedCommandApi feedCommands,
    IScheduledTaskQueryApi taskQueries,
    IStrategyPositionCommandApi positions,
    ScheduledTaskEventCompletion completion,
    ScheduledTaskBusinessReceipts receipts) : OneShotScheduledTaskWorker(lifetime, outcome, logger)
{
    protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
    {
        await actorProducer.StartAsync(
            new ActorMailboxId(ActorType.Query, "FuturesMarketClose"),
            stoppingToken).ConfigureAwait(false);
        try
        {
            await receipts.WaitForRunningAsync(stoppingToken);
            using var closeDeadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            closeDeadline.CancelAfter(TimeSpan.FromMinutes(10));
            var session = await marketQueries.GetMarketSessionAsync().WaitAsync(closeDeadline.Token);
            if (!session.Success || session.Value is null) throw new InvalidOperationException("Market session could not be loaded: " + session.ErrorMessage);
            var valueDate = session.Value.OperationalValueDate;
            var closeBoundary = FuturesTradingValueDate.GetSessionEndUtc(valueDate).UtcDateTime;
            if (session.Value.IsMarketOpen || closeBoundary > DateTime.UtcNow)
                throw new InvalidOperationException("Market close requires an ended session; the operational date cannot advance early.");
            await completion.ExecuteAsync<MarketDataFeedStoppedCompleteEvent,MarketDataFeedStoppedFailEvent>(
                MarketDataFeedStoppedCompleteEvent.Actor, MarketDataFeedStoppedCompleteEvent.Verb, MarketDataFeedStoppedFailEvent.Verb,
                () => feedCommands.StopMarketDataFeedAsync(valueDate), closeDeadline.Token);
            await receipts.RecordAsync(valueDate, "FeedsStopped", "Correlated market feed stop completed before any EOD command.", closeDeadline.Token);
            byte[]? pagingState = null;
            var finalized = new HashSet<Guid>();
            do
            {
                var page = await taskQueries.GetScheduledMarketPositionsAsync(new() { ValueDate = valueDate, PageSize = 100, PagingState = pagingState }, closeDeadline.Token);
                if (!page.Success || page.Value is null) throw new InvalidOperationException("Open positions could not be loaded: " + page.ErrorMessage);
                foreach (var position in page.Value.Positions)
                {
                    if (!finalized.Add(position.Id.PositionId)) continue;
                    var result = await positions.EndOfDayAsync(position.Id, position.StrategyKind, valueDate, closeBoundary, closeDeadline.Token);
                    if (!result.Success) throw new InvalidOperationException($"EOD source commitment failed for {position.Id.Format()}: {result.ErrorMessage}");
                }
                pagingState = page.Value.PagingState;
            } while (pagingState is { Length: > 0 });
            await receipts.RecordAsync(valueDate, "PositionsFinalized", $"All {finalized.Count} position EOD commands committed successfully. History projection is independently reported.", closeDeadline.Token);
            logger.LogInformation("{Component}.{Method} EOD source commitments completed for {ValueDate} {PositionCount}; operational date may now advance.", nameof(Worker), nameof(ExecuteTaskAsync), valueDate, finalized.Count);
            Exception? purgeFailure = null;
            try
            {
                var purgeResult = await jetStreamMaintenance
                    .PurgeCompletedMessagesAsync(stoppingToken)
                    .ConfigureAwait(false);
                logger.LogInformation(
                    "End-of-day JetStream maintenance purged {MessageCount} completed transport messages from {StreamCount} production streams.",
                    purgeResult.PurgedMessages,
                    purgeResult.Streams.Count);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                purgeFailure = exception;
                logger.LogError(
                    exception,
                    "End-of-day JetStream maintenance failed. The blocked stream and all later streams were retained; database backups will still be submitted.");
            }

            var protectionSets = configuration
                .GetSection("DatabaseBackup:ProtectionSets")
                .Get<string[]>() ?? [];
            if (protectionSets.Length == 0)
                throw new InvalidOperationException("At least one scheduled database protection set must be configured.");

            var environmentIdentity = configuration["DatabaseBackup:EnvironmentIdentity"]
                ?? throw new InvalidOperationException("The scheduled database-backup environment identity is missing.");
            var destination = configuration["DatabaseBackup:Destination"] ?? "online-vault";
            var requestedMode = ParseBackupMode(configuration["DatabaseBackup:Mode"]);
            foreach (var protectionSet in protectionSets)
            {
                var requestId = Guid.NewGuid();
                var result = await databaseBackupCommandApi.RequestBackupAsync(
                    new RequestDatabaseBackupCommand
                    {
                        Request = new DatabaseRequestEnvelope
                        {
                            RequestId = requestId,
                            CallerIdentity = "futures-market-close",
                            AuthorizationReference = "scheduled-task-policy",
                            CallerRoles = ["database-backup-operator"],
                            Origin = DatabaseRequestOrigin.ScheduledTask,
                            CorrelationId = requestId,
                            EnvironmentIdentity = environmentIdentity,
                            CreatedUtc = DateTimeOffset.UtcNow
                        },
                        Source = BackupSource.LocalWorkstation,
                        ProtectionSetId = new DatabaseProtectionSetId(protectionSet),
                        ConsistencyMode = DatabaseConsistencyMode.EngineConsistent,
                        RequestedBackupMode = requestedMode,
                        RequiredDestinations = [new DatabaseLogicalDestination(destination, true)]
                    },
                    stoppingToken).ConfigureAwait(false);

                if (!result.Success || result.Value is null)
                    throw new InvalidOperationException(
                        $"The scheduled protection-set backup was rejected: {result.ErrorMessage}");
                logger.LogInformation(
                    "Accepted scheduled database backup {OperationId} for protection set {ProtectionSet}.",
                    result.Value.OperationId.Format(),
                    protectionSet);
            }

            if (purgeFailure is not null)
                throw new InvalidOperationException(
                    "End-of-day JetStream maintenance failed after database backups were submitted.",
                    purgeFailure);
        }
        finally
        {
            await actorProducer.StopAsync().ConfigureAwait(false);
        }
    }

    static DatabaseBackupMode ParseBackupMode(string? value)
        => (value ?? "full").ToLowerInvariant() switch
        {
            "automatic" or "auto" => DatabaseBackupMode.Automatic,
            "full" => DatabaseBackupMode.Full,
            "incremental" => DatabaseBackupMode.Incremental,
            var unsupported => throw new InvalidOperationException(
                $"The scheduled database-backup mode '{unsupported}' is unsupported.")
        };
}
