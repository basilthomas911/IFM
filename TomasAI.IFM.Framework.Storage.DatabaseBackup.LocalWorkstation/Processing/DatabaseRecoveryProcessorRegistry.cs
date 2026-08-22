using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Processing;

public sealed class DatabaseRecoveryProcessorRegistry : IDatabaseRecoveryProcessorRegistry, IDatabaseRecoveryOperationExecutor
{
    readonly IReadOnlyDictionary<BackupSource, IDatabaseRecoveryProcessor> _processors;

    public DatabaseRecoveryProcessorRegistry(IEnumerable<IDatabaseRecoveryProcessor> processors)
    {
        ArgumentNullException.ThrowIfNull(processors);
        var values = processors.ToArray();
        if (values.Any(static processor => processor.Source == BackupSource.None || !Enum.IsDefined(processor.Source)))
            throw new InvalidOperationException("Every DatabaseBackup processor must register a concrete source.");
        if (values.GroupBy(static processor => processor.Source).Any(static group => group.Count() != 1))
            throw new InvalidOperationException("Only one DatabaseBackup processor can be registered for each source.");
        _processors = values.ToDictionary(static processor => processor.Source);
    }

    public IDatabaseRecoveryProcessor GetRequired(BackupSource source)
        => source != BackupSource.None && Enum.IsDefined(source) && _processors.TryGetValue(source, out var processor)
            ? processor
            : throw new UnsupportedDatabaseBackupSourceException(source);

    public ValueTask ExecuteAsync(RecoverableJournalOperation operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        var processor = GetRequired(operation.Intent.Source);
        return processor is IDatabaseRecoveryOperationExecutor executor
            ? executor.ExecuteAsync(operation, cancellationToken)
            : ValueTask.FromException(new NotSupportedException(
                $"The '{processor.Source}' processor has not enabled durable execution yet."));
    }
}
