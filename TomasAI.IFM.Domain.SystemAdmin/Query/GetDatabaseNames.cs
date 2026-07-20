using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.Queries;
using TomasAI.IFM.Shared.SystemAdmin.ViewModels;

namespace TomasAI.IFM.Domain.SystemAdmin.Actor.Query;

public static class GetDatabaseNames
{
    /// <summary>
    /// Handles the GetDatabaseNamesQuery and returns a list of database names.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="context">The query actor context.</param>
    /// <returns>A value task that completes after the reply has been posted.</returns>
    public static async ValueTask GetDatabaseNamesAsync(this GetDatabaseNamesQuery query, IQueryActorContext context)
    {
        var result = new DatabaseNamesReadModel
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

        await context.ReplyAsync(query.Subject.ThreadId, GetDatabaseNamesQuery.Verb, new ServiceResult<DatabaseNamesReadModel>(result));
    }
}
