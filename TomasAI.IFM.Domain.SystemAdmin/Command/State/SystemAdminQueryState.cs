using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.ViewModels;

namespace TomasAI.IFM.Domain.SystemAdmin.Actor.Command.State;

/// <summary>
/// Represents the state for a system admin query actor, managing the retrieval of
/// system administration data such as database names.
/// </summary>
public class SystemAdminQueryState
    : BaseQueryActorState<SystemAdminQueryState>
{
    public override ActorThreadId Id { get; set; } = default!;

    public SystemAdminQueryState As => this;

    /// <summary>
    /// Retrieves the names of all available databases that can be backed up.
    /// </summary>
    /// <returns>A <see cref="DatabaseNamesReadModel"/> containing the list of database names.</returns>
    public static DatabaseNamesReadModel     GetDatabaseNames()
        => new ()
        {
            Names =
            [
                DatabaseBackupNames.EventDb,
                DatabaseBackupNames.FundDb,
                DatabaseBackupNames.LogDb,
                DatabaseBackupNames.MarketDataDb,
                DatabaseBackupNames.OptionPricerDb,
                DatabaseBackupNames.ReferenceDb,
                DatabaseBackupNames.TradeDb
            ]
        };
}
