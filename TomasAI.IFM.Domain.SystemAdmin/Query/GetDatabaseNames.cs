using TomasAI.IFM.Domain.SystemAdmin.Shared.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ViewModels;
using TomasAI.IFM.Domain.SystemAdmin.Command.State;

namespace TomasAI.IFM.Domain.SystemAdmin.Query;

public static class GetDatabaseNames
{
    /// <summary>
    /// Resolves the cached, read-only list of database names.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <returns>The immutable database-name read model.</returns>
    public static DatabaseNamesReadModel ResolveDatabaseNames(this GetDatabaseNamesQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return SystemAdminQueryState.GetDatabaseNames();
    }
}
