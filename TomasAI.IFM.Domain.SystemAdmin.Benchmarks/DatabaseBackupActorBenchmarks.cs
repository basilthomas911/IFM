using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Event.Translation;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.SystemAdmin.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net10_0, warmupCount: 3, iterationCount: 8)]
public class DatabaseBackupActorBenchmarks
{
    DatabaseBackupCommandState _state = default!;
    RecordDatabaseOperationAdmissionCommand _duplicate = default!;
    DatabaseBackupServiceProgressEvent _progress = default!;

    [GlobalSetup]
    public void Setup()
    {
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        var request = Request(operationId.Value);
        _state = new DatabaseBackupCommandState();
        _state.Execute(new RequestDatabaseBackupCommand
        {
            CommandId = request.RequestId, EntityId = operationId, Request = request,
            Source = BackupSource.LocalWorkstation, ProtectionSetId = new DatabaseProtectionSetId("core"),
            ConsistencyMode = DatabaseConsistencyMode.EngineConsistent,
            RequiredDestinations = [new DatabaseLogicalDestination("vault", true)]
        });
        var source = Source(operationId, 1, DatabaseRecoveryPhase.Admitted);
        _duplicate = new RecordDatabaseOperationAdmissionCommand
        {
            CommandId = source.SourceEventId, EntityId = operationId, Source = source,
            Subject = new ActorSubject(ActorType.Command, DatabaseBackupCommand.Actor, "RecordAdmission", operationId.Format())
        };
        _state.Execute(_duplicate);
        var progressSource = Source(operationId, 2, DatabaseRecoveryPhase.Capturing);
        _progress = new DatabaseBackupServiceProgressEvent
        {
            Id = progressSource.SourceEventId, EntityId = operationId, CommandId = progressSource.CorrelationId,
            Source = progressSource, ProgressPercent = 50,
            Subject = new ActorSubject(ActorType.Event, "DatabaseBackupEvent", "BackupProgress", operationId.Format()),
            ReceivedOn = progressSource.ObservedUtc.UtcDateTime
        };
    }

    [Benchmark]
    public DatabaseRecoveryOperationId ExactDuplicateAdmission() => _state.Execute(_duplicate);

    [Benchmark]
    public DatabaseBackupInternalCommand TranslateProgress() => DatabaseBackupEventTranslator.Translate(_progress);

    static DatabaseRequestEnvelope Request(Guid id) => new()
    {
        RequestId = id, CallerIdentity = "benchmark", AuthorizationReference = "benchmark",
        CallerRoles = ["DatabaseRecoveryOperator"], Origin = DatabaseRequestOrigin.Console,
        CorrelationId = Guid.NewGuid(), EnvironmentIdentity = "benchmark", CreatedUtc = DateTimeOffset.UtcNow
    };

    static DatabaseSourceEnvelope Source(DatabaseRecoveryOperationId operationId, long sequence, DatabaseRecoveryPhase phase) => new()
    {
        SourceEventId = Guid.NewGuid(), OperationId = operationId, Source = BackupSource.LocalWorkstation,
        ProtectionSetId = new DatabaseProtectionSetId("core"), OperationKind = DatabaseRecoveryOperationKind.Backup,
        Phase = phase, ProducingHostId = new DatabaseBackupHostId("benchmark-host"), SourceRevisionOrSequence = sequence,
        CorrelationId = Guid.NewGuid(), CausationId = Guid.NewGuid(), ObservedUtc = DateTimeOffset.UtcNow
    };
}
