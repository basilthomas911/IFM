using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Reference.EconomicCalendar.Query;

public static class GetEconomicCalendarAll
{
    /// <summary>
    /// Handles the GetEconomicCalendarAllQuery by retrieving all economic calendar entries from the database and replying with the results.
    /// </summary>
    /// <param name="q">The query requesting all economic calendar entries.</param>
    /// <param name="context">The query actor context for replying with results.</param>
    /// <param name="dbFactory">The database context factory used to access reference storage.</param>
    /// <returns>A value task that completes after the reply has been posted.</returns>
    public static async ValueTask<EconomicCalendarReadModel[]> GetEconomicCalendarAllAsync(
        this GetEconomicCalendarAllQuery q, IDbContextFactory dbFactory, CancellationToken cancellationToken = default)
        => [.. await (cancellationToken.CanBeCanceled
            ? dbFactory.ReferenceDb.GetEconomicCalendarAllAsync(cancellationToken)
            : dbFactory.ReferenceDb.GetEconomicCalendarAllAsync())];
}
