using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.Shared;
using TomasAI.IFM.Domain.SystemAdmin.Shared.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ViewModels;
using TomasAI.IFM.Domain.SystemAdmin.Command.State;

namespace TomasAI.IFM.Domain.SystemAdmin.Query;

public static class GetDatabaseNames
{
    /// <summary>
    /// Handles the GetDatabaseNamesQuery and returns a list of database names.
    /// </summary>
    /// <param name="query">The query to handle.</param>
    /// <param name="context">The query actor context.</param>
    /// <returns>A value task that completes after the reply has been posted.</returns>
    public static ValueTask<DatabaseNamesReadModel> GetDatabaseNamesAsync(this GetDatabaseNamesQuery query)
        => ValueTask.FromResult(SystemAdminQueryState.GetDatabaseNames());
}
