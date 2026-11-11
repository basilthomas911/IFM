using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.EconomicCalendar.Query;

public static class GetEconomicCalendarPage
{

    extension(GetEconomicCalendarPageQuery query)
    {
        public async ValueTask<EconomicCalendarPageReadModel> ExecuteAsync(
            IDbContextFactory dbFactory,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dbFactory);
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(query.Request);
            query.Request.Validate();
            return await dbFactory.MarketDataDb
                .GetEconomicCalendarPageAsync(query.Request, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
