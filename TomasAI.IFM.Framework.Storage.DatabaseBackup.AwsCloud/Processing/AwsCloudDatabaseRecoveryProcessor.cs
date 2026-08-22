using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Processing;

public sealed class AwsCloudDatabaseRecoveryProcessor(
    IDatabaseBackupExecutionJournal journal,
    AwsCloudDatabaseBackupOptions options) : IDatabaseRecoveryProcessor
{
    public BackupSource Source => BackupSource.AwsCloud;

    public async ValueTask<DatabaseExecutionAdmission> AdmitAsync(
        DatabaseExecutionIntent intent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);
        intent.Validate();
        if (intent.Source != Source) throw new UnsupportedDatabaseBackupSourceException(intent.Source);
        if (!options.AcceptBackupRequests)
            throw new AwsCloudRequestAdmissionDisabledException();
        var result = await journal.AdmitAsync(intent, cancellationToken).ConfigureAwait(false);
        return new(result.OperationId, result.Outcome == JournalAdmissionOutcome.Admitted
            ? DatabaseExecutionAdmissionOutcome.Admitted
            : DatabaseExecutionAdmissionOutcome.ExactDuplicate);
    }
}

public sealed class AwsCloudRequestAdmissionDisabledException()
    : InvalidOperationException("AWS cloud backup request admission remains disabled until the orchestration gate is qualified.");
