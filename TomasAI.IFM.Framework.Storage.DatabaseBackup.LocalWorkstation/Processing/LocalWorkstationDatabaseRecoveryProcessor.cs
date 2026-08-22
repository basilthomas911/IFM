using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Processing;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Processing;

public sealed class LocalWorkstationDatabaseRecoveryProcessor
    : IDatabaseRecoveryProcessor, IDatabaseRecoveryOperationExecutor, IDatabaseRecoveryProcessorRouting
{
    readonly IDatabaseRecoveryEngineSelector _selector;
    readonly DatabaseRecoveryOperationOrchestrator _orchestrator;

    public LocalWorkstationDatabaseRecoveryProcessor(
        IDatabaseBackupExecutionJournal journal,
        IPostgreSqlBackupCapability postgreSql,
        DatabaseBackupHostOptions options)
        : this(journal, postgreSql, new FakeScyllaBackupCapability(),
            new PostgreSqlOnlyDatabaseRecoveryEngineSelector(), options,
            new FakeDatabaseBackupPublicationCapability(), new FakeDatabaseRestoreSourceCapability(),
            new FakeDatabaseRecoveryEvidenceStore(), new FakeDatabaseBackupChainPlanner()) { }

    public LocalWorkstationDatabaseRecoveryProcessor(
        IDatabaseBackupExecutionJournal journal,
        IPostgreSqlBackupCapability postgreSql,
        IScyllaBackupCapability scylla,
        IDatabaseRecoveryEngineSelector engineSelector,
        DatabaseBackupHostOptions options)
        : this(journal, postgreSql, scylla, engineSelector, options,
            new FakeDatabaseBackupPublicationCapability(), new FakeDatabaseRestoreSourceCapability(),
            new FakeDatabaseRecoveryEvidenceStore(), new FakeDatabaseBackupChainPlanner()) { }

    public LocalWorkstationDatabaseRecoveryProcessor(
        IDatabaseBackupExecutionJournal journal,
        IPostgreSqlBackupCapability postgreSql,
        IScyllaBackupCapability scylla,
        IDatabaseRecoveryEngineSelector engineSelector,
        DatabaseBackupHostOptions options,
        IDatabaseBackupPublicationCapability publication,
        IDatabaseRestoreSourceCapability restoreSources,
        IDatabaseRecoveryEvidenceStore evidence,
        IDatabaseBackupChainPlanner chainPlanner)
    {
        _selector = engineSelector ?? throw new ArgumentNullException(nameof(engineSelector));
        _orchestrator = new DatabaseRecoveryOperationOrchestrator(
            BackupSource.LocalWorkstation, journal, postgreSql, scylla, engineSelector,
            options, publication, restoreSources, evidence, chainPlanner);
    }

    public BackupSource Source => BackupSource.LocalWorkstation;
    public bool CanProcess(DatabaseProtectionSetId protectionSetId) => _selector.CanSelect(protectionSetId);
    public ValueTask<DatabaseExecutionAdmission> AdmitAsync(DatabaseExecutionIntent intent, CancellationToken cancellationToken)
        => _orchestrator.AdmitAsync(intent, cancellationToken);
    public ValueTask ExecuteAsync(RecoverableJournalOperation operation, CancellationToken cancellationToken)
        => _orchestrator.ExecuteAsync(operation, cancellationToken);
}
