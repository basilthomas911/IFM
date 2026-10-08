using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query;
/// <summary>Enumerates current open positions solely from persisted ScyllaDB routes and projections.</summary>
public static class GetScheduledMarketPositions
{
    /// <summary>Reads a bounded route page, resolves current positions and returns the next continuation.</summary>
    public static async ValueTask ExecuteAsync(this GetScheduledMarketPositionsQuery query, ITradeDbReadContext store,
        IQueryActorContext<ScheduledTaskQueryActor> context, CancellationToken cancellationToken)
    {
        var page = await store.GetOpenPositionRoutePageAsync(query.PageSize, query.PagingState, cancellationToken);
        var positions = new List<StrategyPositionSnapshot>();
        foreach (var route in page.Items.Select(item => item.Route).DistinctBy(item => item.StrategyPositionId))
        {
            var id = new StrategyPositionId(new(route.PortfolioId,route.FundId,route.OrderId,route.TradeId), route.StrategyPositionId);
            var position = await store.GetStrategyPositionAsync(id, cancellationToken);
            if (position is { IsOpen: true } && (query.ValueDate == default || position.ValueDate <= query.ValueDate)) positions.Add(position);
        }
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<ScheduledMarketPositionPage>(new()
        { Positions = positions.ToArray(), PagingState = page.PagingState }));
    }
}
