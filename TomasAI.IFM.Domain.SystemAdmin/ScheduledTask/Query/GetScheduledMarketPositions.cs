using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query;
/// <summary>Enumerates current open positions solely from persisted ScyllaDB routes and projections.</summary>
public static class GetScheduledMarketPositions
{

    extension(GetScheduledMarketPositionsQuery query)
    {
        /// <summary>Reads a bounded route page, resolves current positions and returns the next continuation.</summary>
        public async ValueTask<ServiceResult<ScheduledMarketPositionPage>> ExecuteAsync(ITradeDbReadContext store, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(store);
            var page = await (store.GetOpenPositionRoutePageAsync(query.PageSize, query.PagingState, cancellationToken)).ConfigureAwait(false);
            var positions = new List<StrategyPositionSnapshot>();
            foreach (var route in page.Items.Select(item => item.Route).DistinctBy(item => item.StrategyPositionId))
            {
                var id = new StrategyPositionId(new(route.PortfolioId, route.FundId, route.OrderId, route.TradeId), route.StrategyPositionId);
                var position = await (store.GetStrategyPositionAsync(id, cancellationToken)).ConfigureAwait(false);
                if (position is { IsOpen: true } && (query.ValueDate == default || position.ValueDate <= query.ValueDate)) positions.Add(position);
            }
            return new ServiceOk<ScheduledMarketPositionPage>(new()
            { Positions = positions.ToArray(), PagingState = page.PagingState });
        }
    }
}
