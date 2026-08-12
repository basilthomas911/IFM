using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EconomicCalendarsDb;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query;

public static class GetExternalEconomicCalendars
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static async ValueTask<EconomicCalendarReadModel[]> GetExternalEconomicCalendarsAsync(
        this GetExternalEconomicCalendarsQuery q, IDbContextFactory dbFactory, CancellationToken cancellationToken = default)
        => await dbFactory.GetExternalEconomicCalendarsAsync(cancellationToken);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    static async ValueTask<EconomicCalendarReadModel[]> GetExternalEconomicCalendarsAsync(
        this IDbContextFactory dbFactory,
        CancellationToken cancellationToken)
    {
        if (dbFactory.EconomicCalendarsDb is not IEconomicCalendarsDbContext ecCal)
            return [];
        return [.. await (cancellationToken.CanBeCanceled
            ? ecCal.ReadAsync(cancellationToken)
            : ecCal.ReadAsync())];
    }
}
