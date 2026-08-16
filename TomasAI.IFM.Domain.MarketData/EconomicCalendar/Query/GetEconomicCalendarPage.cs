using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query;

public static class GetEconomicCalendarPage
{
    public static async ValueTask<EconomicCalendarPageReadModel> GetEconomicCalendarPageAsync(
        this GetEconomicCalendarPageQuery query,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        query.Request.Validate();
        return await dbFactory.MarketDataDb
            .GetEconomicCalendarPageAsync(query.Request, cancellationToken)
            .ConfigureAwait(false);
    }
}
