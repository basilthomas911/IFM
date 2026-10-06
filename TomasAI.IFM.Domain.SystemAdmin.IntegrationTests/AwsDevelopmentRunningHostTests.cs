using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Execution;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit.Abstractions;

namespace TomasAI.IFM.Domain.SystemAdmin.IntegrationTests;

public sealed class AwsDevelopmentRunningHostTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "LiveAwsRunningHost")]
    public async Task Native_PostgreSql_backup_command_completes_through_running_host_and_AWS()
    {
        if (Environment.GetEnvironmentVariable("IFM_AWS_HOST_SMOKE") != "1") return;
        var operation = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var state = new DatabaseBackupCommandState();
        var request = new DatabaseRequestEnvelope
        {
            RequestId = Guid.NewGuid(), CallerIdentity = "development-backup-smoke",
            AuthorizationReference = "development-backup-restore-activation-20261004",
            CallerRoles = ["DatabaseRecoveryOperator"], Origin = DatabaseRequestOrigin.Console,
            CorrelationId = Guid.NewGuid(), EnvironmentIdentity = "development", CreatedUtc = DateTimeOffset.UtcNow
        };
        state.Execute(new RequestDatabaseBackupCommand
        {
            CommandId = request.RequestId, EntityId = operation, Request = request, Source = BackupSource.AwsCloud,
            ProtectionSetId = new DatabaseProtectionSetId("core-postgresql"), ExpectedPolicyRevision = 1,
            ConsistencyMode = DatabaseConsistencyMode.EngineConsistent,
            RequiredDestinations = [new DatabaseLogicalDestination("aws-primary", true)], RequestedBackupMode = DatabaseBackupMode.Full
        });
        var domain = Assert.Single(state.Events.OfType<DatabaseBackupExecutionRequestedDomainEvent>());
        var execution = Assert.IsType<DatabaseBackupExecutionRequestedEvent>(DatabaseBackupStateRepository.ToExecutionEvent(domain));
        output.WriteLine("OperationId={0}", operation.Format());
        var completed = new TaskCompletionSource<DatabaseBackupServiceCompletedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new List<string>();
        var connectionManager = new NatsConnectionManager();
        var listener = new NatsJetStreamEventListener(new NatsJetStreamEventListenerOptions
        {
            Url = "nats://127.0.0.1:4222", StreamName = "EventStream",
            DurableConsumerNamePrefix = "aws-host-smoke-" + operation.Format(), DeliverPolicy = NatsJetStreamEventDeliverPolicy.New,
            DispatcherCount = 1, DispatcherCapacity = 16, MaxAckPending = 16, MaxMessages = 16, ThresholdMessages = 1
        }, NullLogger<NatsJetStreamEventListener>.Instance);
        var producer = new NatsJetStreamActorProducer(new NatsJetStreamProducerOptions { Url = "nats://127.0.0.1:4222" },
            NullLogger<NatsJetStreamActorProducer>.Instance, connectionManager);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        try
        {
            await listener.StartAsync("aws-smoke-" + operation.Format(), new Dictionary<ActorMailboxId, List<string>>
            {
                [new ActorMailboxId(ActorType.Event, "DatabaseBackupEvent")] = ["BackupAccepted", "BackupStarted", "BackupBoundaryEstablished", "BackupVerificationCompleted", "BackupArtifactReplicaUpdated", "RunStatisticsCaptured", "BackupCompleted", "BackupFailed"]
            }, (verb, message) =>
            {
                // All service events share the inherited numeric-key contract; only inspect matching operation IDs.
                var service = message.AsEvent<DatabaseBackupServiceCompletedEvent>();
                if (service?.EntityId != operation) return ValueTask.CompletedTask;
                observed.Add(verb);
                if (verb == "BackupFailed") completed.TrySetException(new InvalidOperationException("Host reported backup failure: " + service.SafeDiagnosticReference));
                if (verb == "BackupCompleted") completed.TrySetResult(service);
                return ValueTask.CompletedTask;
            });
            await producer.StartAsync(execution.Subject.ActorId, timeout.Token);
            await producer.SendAsync<DatabaseBackupExecutionRequestedEvent, DatabaseRecoveryOperationId>(execution.Subject, execution, timeout.Token);
            var result = await completed.Task.WaitAsync(timeout.Token);
            Assert.Equal(DatabaseRecoveryOutcome.Succeeded, result.Outcome);
            Assert.NotNull(result.RestorePointId);
            var evidenceRoot = Environment.GetEnvironmentVariable("IFM_AWS_HOST_EVIDENCE") ?? throw new InvalidOperationException("Evidence path required.");
            Directory.CreateDirectory(evidenceRoot);
            await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "backup-completed.json"), JsonSerializer.Serialize(new
            { operationId = operation.Format(), result.RestorePointId, result.Outcome, result.BackupLineage, observed }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            await producer.StopAsync(CancellationToken.None);
            await listener.StopAsync();
            await connectionManager.DisposeAsync();
        }
    }
}
