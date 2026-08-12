using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Net;
using TomasAI.IFM.Api.DatabaseBackup.Host;
using TomasAI.IFM.Api.DatabaseBackup.Host.Services;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Translation;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Journal;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Processing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.SystemAdmin.IntegrationTests;

public sealed class DatabaseBackupHostEndToEndTests
{
    static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    [Trait("Category", "Gate5Integration")]
    public async Task Host_composition_preserves_safety_critical_startup_order()
    {
        var journalPath = Path.Combine(Path.GetTempPath(), "ifm-gate5-registration", Guid.NewGuid().ToString("N"), "journal.db");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DatabaseBackup:Host:HostId"] = "gate5-registration-host",
            ["DatabaseBackup:Journal:Path"] = journalPath,
            ["DatabaseBackup:Journal:RequirePersistentPath"] = "false",
            ["Nats:JetStreamEventListener:StreamName"] = $"IFM_GATE5_{Guid.NewGuid():N}",
            ["Nats:JetStreamEventListener:DurableConsumerNamePrefix"] = "gate5-registration"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDatabaseBackupHost(configuration);
        await using var provider = services.BuildServiceProvider();

        var hostedServices = provider.GetServices<IHostedService>()
            .Select(static service => service.GetType())
            .Where(static type => type.Namespace?.StartsWith("TomasAI.IFM.Api.DatabaseBackup.Host", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Equal(
        [
            typeof(DatabaseBackupJournalInitializationService),
            typeof(DatabaseBackupOutboxPublisher),
            typeof(DatabaseBackupStartupReconciliationService),
            typeof(DatabaseBackupExecutionDispatcher),
            typeof(DatabaseBackupInboundListener)
        ], hostedServices);
        Assert.NotNull(provider.GetService<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckService>());
    }

    [Fact]
    [Trait("Category", "Gate5Integration")]
    public async Task Fake_operation_survives_host_restart_replays_outbox_and_updates_projection()
    {
        var natsUrl = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";
        var postgres = Environment.GetEnvironmentVariable("IFM_POSTGRES_TEST_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=event-source-test-db";
        var resourceId = Guid.NewGuid().ToString("N");
        var streamName = $"IFM_GATE5_{resourceId}";
        var tempDirectory = Path.Combine(Path.GetTempPath(), "ifm-gate5-e2e", resourceId);
        var journalPath = Path.Combine(tempDirectory, "execution-journal.db");
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        var command = BackupCommand(operationId);
        state.Execute(command);
        var publicEvents = state.Events.OfType<DatabaseBackupEventContract>().ToArray();
        var executionDomainEvent = Assert.IsType<DatabaseBackupExecutionRequestedDomainEvent>(publicEvents.Last());
        var workOrder = Assert.IsType<DatabaseBackupExecutionRequestedEvent>(
            DatabaseBackupStateRepository.ToExecutionEvent(executionDomainEvent));
        state.Events.Clear();

        var dbSettings = new DbConnectionSettings()
            .Add(SystemAdminDbContext.SystemAdminDbConnection, postgres, "System.Data.Postgres");
        var storageLogger = NullLogger<DbProvider>.Instance;
        await new SystemAdminSchemaDb(dbSettings, storageLogger).CreateAllAsync();
        var projection = new SystemAdminDbContext(dbSettings, storageLogger);
        const string projectorName = "Gate5EndToEnd";

        var hostOptions = new LocalWorkstationDatabaseBackupOptions
        {
            HostId = "gate5-e2e-host",
            LeaseDuration = TimeSpan.FromSeconds(30),
            PollInterval = TimeSpan.FromMilliseconds(20),
            OutboxBatchSize = 16
        };
        var journalOptions = new DatabaseBackupJournalOptions
        {
            Path = journalPath,
            RequirePersistentPath = false,
            BusyTimeoutMilliseconds = 2_000
        };
        var connectionManager = new NatsConnectionManager();
        var executionListener = Listener(natsUrl, streamName, $"gate5-in-{resourceId}");
        var serviceListener = Listener(natsUrl, streamName, $"gate5-out-{resourceId}");
        await using var admin = new NatsClient(natsUrl);
        await admin.ConnectAsync();
        var jetStream = admin.CreateJetStreamContext();

        try
        {
            await projection.ClearDatabaseBackupProjectionsAsync(projectorName);
            var revision = 0L;
            foreach (var domainEvent in publicEvents)
                await projection.ApplyDatabaseBackupEventAsync(
                    projectorName, domainEvent with { EventId = ++revision });

            var firstJournal = Journal(journalOptions, hostOptions);
            await firstJournal.InitializeAsync(CancellationToken.None);
            var firstProcessor = Processor(firstJournal, hostOptions);
            var inbound = new DatabaseBackupInboundListener(
                executionListener,
                new DatabaseRecoveryProcessorRegistry([firstProcessor]),
                new DatabaseBackupHostHealthState());
            var executionProducer = new NatsJetStreamActorProducer(
                new NatsJetStreamProducerOptions { Url = natsUrl },
                NullLogger<NatsJetStreamActorProducer>.Instance,
                connectionManager);
            await inbound.StartAsync(CancellationToken.None);
            await executionProducer.StartAsync(workOrder.Subject.ActorId, CancellationToken.None);
            await executionProducer.SendAsync<DatabaseBackupExecutionRequestedEvent, DatabaseRecoveryOperationId>(
                workOrder.Subject, workOrder, CancellationToken.None);
            await WaitUntilAsync(async () => (await PendingAsync(firstJournal)).Count == 1);

            await inbound.StopAsync(CancellationToken.None);
            await executionProducer.StopAsync(CancellationToken.None);

            var restartedJournal = Journal(journalOptions, hostOptions);
            await restartedJournal.InitializeAsync(CancellationToken.None);
            await restartedJournal.VerifyIntegrityAsync(CancellationToken.None);
            var restartedProcessor = Processor(restartedJournal, hostOptions);
            var dispatcher = new DatabaseBackupExecutionDispatcher(
                restartedJournal, restartedProcessor, hostOptions,
                NullLogger<DatabaseBackupExecutionDispatcher>.Instance);
            Assert.Equal(1, await dispatcher.DispatchOnceAsync(CancellationToken.None));
            Assert.Equal(5, (await PendingAsync(restartedJournal)).Count);

            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await serviceListener.StartAsync(
                $"gate5-core-{resourceId}",
                new Dictionary<ActorMailboxId, List<string>>
                {
                    [new ActorMailboxId(ActorType.Event, "DatabaseBackupEvent")] =
                    ["BackupAccepted", "BackupStarted", "BackupBoundaryEstablished", "BackupVerificationCompleted", "BackupCompleted"]
                },
                async (verb, message) =>
                {
                    var serviceEvent = ServiceEvent(verb, message);
                    state.Execute(DatabaseBackupEventTranslator.Translate(serviceEvent));
                    var domainEvent = Assert.IsAssignableFrom<DatabaseBackupEventContract>(state.Events.Single());
                    state.Events.Clear();
                    await projection.ApplyDatabaseBackupEventAsync(
                        projectorName, domainEvent with { EventId = Interlocked.Increment(ref revision) });
                    if (domainEvent is DatabaseOperationCompletedEvent) completed.TrySetResult();
                });

            var transport = new JetStreamDatabaseBackupServiceEventTransport(
                new NatsJetStreamActorProducer(
                    new NatsJetStreamProducerOptions { Url = natsUrl },
                    NullLogger<NatsJetStreamActorProducer>.Instance,
                    connectionManager));
            var outbox = new DatabaseBackupOutboxPublisher(
                restartedJournal, transport, hostOptions,
                NullLogger<DatabaseBackupOutboxPublisher>.Instance);
            await transport.StartAsync(CancellationToken.None);
            Assert.Equal(5, await outbox.PublishPendingOnceAsync(CancellationToken.None));
            await completed.Task.WaitAsync(TestTimeout);
            await transport.StopAsync(CancellationToken.None);
            await serviceListener.StopAsync();

            Assert.Empty(await PendingAsync(restartedJournal));
            Assert.Equal(DatabaseRecoveryPhase.Completed, state.Operation.Phase);
            Assert.Equal(DatabaseRecoveryOutcome.Succeeded, state.Operation.Outcome);
            var readModel = await projection.GetBackupOperationAsync(new GetDatabaseBackupOperationQuery
            {
                EntityId = operationId,
                OperationId = operationId
            }, CancellationToken.None);
            Assert.NotNull(readModel);
            Assert.Equal(DatabaseRecoveryPhase.Completed, readModel.Phase);
            Assert.Equal(DatabaseRecoveryOutcome.Succeeded, readModel.Outcome);
        }
        finally
        {
            await executionListener.StopAsync();
            await serviceListener.StopAsync();
            await connectionManager.DisposeAsync();
            await projection.ClearDatabaseBackupProjectionsAsync(projectorName);
            await TryDeleteStreamAsync(jetStream, streamName);
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, recursive: true);
        }
    }

    static RequestDatabaseBackupCommand BackupCommand(DatabaseRecoveryOperationId operationId)
    {
        var request = new DatabaseRequestEnvelope
        {
            RequestId = Guid.NewGuid(),
            CallerIdentity = "gate5-operator",
            AuthorizationReference = "gate5-approval",
            CallerRoles = ["DatabaseRecoveryOperator"],
            Origin = DatabaseRequestOrigin.Console,
            CorrelationId = Guid.NewGuid(),
            EnvironmentIdentity = "paper-trading",
            CreatedUtc = DateTimeOffset.UtcNow
        };
        return new RequestDatabaseBackupCommand
        {
            CommandId = request.RequestId,
            EntityId = operationId,
            Request = request,
            Source = BackupSource.LocalWorkstation,
            ProtectionSetId = new DatabaseProtectionSetId("gate5-core"),
            ConsistencyMode = DatabaseConsistencyMode.CoordinatedProtectionSet,
            RequiredDestinations = [new DatabaseLogicalDestination("fake-vault", true)],
            ExpectedPolicyRevision = 1
        };
    }

    static SqliteDatabaseBackupExecutionJournal Journal(
        DatabaseBackupJournalOptions journalOptions,
        LocalWorkstationDatabaseBackupOptions hostOptions)
        => new(journalOptions, hostOptions, NullLogger<SqliteDatabaseBackupExecutionJournal>.Instance);

    static LocalWorkstationDatabaseRecoveryProcessor Processor(
        IDatabaseBackupExecutionJournal journal,
        LocalWorkstationDatabaseBackupOptions options)
        => new(journal, new FakePostgreSqlBackupCapability(), options);

    static NatsJetStreamEventListener Listener(string url, string streamName, string durablePrefix) => new(
        new NatsJetStreamEventListenerOptions
        {
            Url = url,
            StreamName = streamName,
            DurableConsumerNamePrefix = durablePrefix,
            DeliverPolicy = NatsJetStreamEventDeliverPolicy.New,
            DispatcherCount = 1,
            DispatcherCapacity = 16,
            MaxAckPending = 16,
            MaxMessages = 16,
            ThresholdMessages = 1,
            AckWait = TimeSpan.FromSeconds(5),
            NegativeAcknowledgeDelay = TimeSpan.FromMilliseconds(100)
        },
        NullLogger<NatsJetStreamEventListener>.Instance);

    static DatabaseBackupServiceEventContract ServiceEvent(string verb, NatsMsg<byte[]> message)
        => verb switch
        {
            "BackupAccepted" => message.AsEvent<DatabaseBackupServiceAcceptedEvent>()!,
            "BackupStarted" => message.AsEvent<DatabaseBackupServiceStartedEvent>()!,
            "BackupBoundaryEstablished" => message.AsEvent<DatabaseBackupBoundaryEstablishedEvent>()!,
            "BackupVerificationCompleted" => message.AsEvent<DatabaseBackupVerificationCompletedEvent>()!,
            "BackupCompleted" => message.AsEvent<DatabaseBackupServiceCompletedEvent>()!,
            _ => throw new InvalidOperationException($"Unexpected service event '{verb}'.")
        };

    static async Task<List<PendingServiceEvent>> PendingAsync(IDatabaseBackupExecutionJournal journal)
    {
        var pending = new List<PendingServiceEvent>();
        await foreach (var item in journal.ReadPendingServiceEventsAsync(100, CancellationToken.None)) pending.Add(item);
        return pending;
    }

    static async Task WaitUntilAsync(Func<Task<bool>> predicate)
    {
        using var timeout = new CancellationTokenSource(TestTimeout);
        while (!await predicate()) await Task.Delay(25, timeout.Token);
    }

    static async ValueTask TryDeleteStreamAsync(INatsJSContext jetStream, string streamName)
    {
        try { await jetStream.DeleteStreamAsync(streamName); }
        catch (NatsJSApiException) { }
    }

}
