using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query;

public static class GetExternalEconomicCalendars
{
    public static ValueTask<EconomicCalendarReadModel[]> GetExternalEconomicCalendarsAsync(
        this GetExternalEconomicCalendarsQuery query,
        IDbContextFactory dbFactory,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        throw new NotSupportedException(
            "The external-calendar compatibility query was retired. Use POST /api/marketdata/fmp/import.");
    }
}
