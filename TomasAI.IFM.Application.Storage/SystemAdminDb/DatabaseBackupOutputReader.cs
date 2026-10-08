using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb;
namespace TomasAI.IFM.Application.Storage;
/// <summary>Reads the configured local or mounted backup output root using bounded UTF-8 pages.</summary>
public sealed class DatabaseBackupOutputReader(string root) : IDatabaseBackupOutputReader
{
    readonly ScheduledTaskOutputReader _reader = new(root);
    /// <inheritdoc />
    public async ValueTask<DatabaseBackupSetupReadModel> ReadSetupAsync(BackupSource source, CancellationToken cancellationToken)
    {
        DatabaseBackupEnumValidation.RequireConcrete(source);
        var page = await _reader.ReadAsync(Path.Combine(source.ToString(), "setup"), 0, cancellationToken);
        return page.Available ? System.Text.Json.JsonSerializer.Deserialize<DatabaseBackupSetupReadModel>(page.Text) ?? new() : new();
    }
    /// <inheritdoc />
    public async ValueTask<DatabaseBackupLogReadModel> ReadAsync(BackupSource source, Guid operationId, long offset, CancellationToken cancellationToken)
    {
        DatabaseBackupEnumValidation.RequireConcrete(source);
        if (operationId == Guid.Empty) throw new ArgumentException("Operation identity is required.", nameof(operationId));
        var page = await _reader.ReadAsync(Path.Combine(source.ToString(), operationId.ToString("N")), offset, cancellationToken);
        return new() { Output = page.Text, NextOutputOffset = page.NextOffset, EndOfOutput = page.EndOfOutput, OutputAvailable = page.Available };
    }
}
