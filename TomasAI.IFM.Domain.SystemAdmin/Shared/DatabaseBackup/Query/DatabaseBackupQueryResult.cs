using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Query;

/// <summary>Builds the typed result of an optional Database Backup projection read.</summary>
internal static class DatabaseBackupQueryResult
{
    internal static ServiceResult<TResult> Optional<TResult>(TResult? result) where TResult : class
        => result is null
            ? new ServiceFailed<TResult>(404, "DatabaseBackup projection was not found.")
            : new ServiceOk<TResult>(result);
}
