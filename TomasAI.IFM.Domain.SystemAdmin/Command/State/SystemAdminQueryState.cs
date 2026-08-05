using TomasAI.IFM.Domain.SystemAdmin.Shared;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ViewModels;

namespace TomasAI.IFM.Domain.SystemAdmin.Command.State;

/// <summary>
/// Provides lookups for system administration data such as database names.
/// </summary>
public static class SystemAdminQueryState
{
    static readonly DatabaseNamesReadModel DatabaseNames = new(
        new[]
        {
            DatabaseBackupNames.EventDb,
            DatabaseBackupNames.FundDb,
            DatabaseBackupNames.LogDb,
            DatabaseBackupNames.MarketDataDb,
            DatabaseBackupNames.OptionPricerDb,
            DatabaseBackupNames.ReferenceDb,
            DatabaseBackupNames.TradeDb
        });

    /// <summary>
    /// Retrieves the names of all available databases that can be backed up.
    /// </summary>
    /// <returns>A <see cref="DatabaseNamesReadModel"/> containing the list of database names.</returns>
    public static DatabaseNamesReadModel GetDatabaseNames() => DatabaseNames;
}
