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
    IConfiguration configuration) : OneShotScheduledTaskWorker(lifetime, outcome, logger)
{
    protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
    {
        await actorProducer.StartAsync(
            new ActorMailboxId(ActorType.Query, "FuturesMarketClose"),
            stoppingToken).ConfigureAwait(false);
        try
        {
            logger.LogInformation("Recording the IFM end-of-day lifecycle request before JetStream maintenance and protection-set backup submission.");
            var shutdownResult = await applicationCommandApi
                .ShutdownApplicationAsync(DateOnly.FromDateTime(DateTime.UtcNow))
                .ConfigureAwait(false);
            if (!shutdownResult.Success)
                throw new InvalidOperationException(
                    $"The application shutdown command was rejected: {shutdownResult.ErrorMessage}");
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
