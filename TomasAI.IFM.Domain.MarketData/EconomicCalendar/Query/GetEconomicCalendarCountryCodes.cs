using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query;

public static class GetEconomicCalendarCountryCodes
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="q"></param>
    /// <param name="context"></param>
    /// <param name="dbFactory"></param>
    /// <returns></returns>
    public static async ValueTask<EconomicCalendarCountryCodeReadModel[]> ExecuteAsync(
        this GetEconomicCalendarCountryCodesQuery q, IDbContextFactory dbFactory, CancellationToken cancellationToken = default)
        
    {
        ArgumentNullException.ThrowIfNull(dbFactory);
        return [.. await (dbFactory.MarketDataDb.GetEconomicCalendarCountryCodesAsync(cancellationToken)).ConfigureAwait(false)];
    }
}
