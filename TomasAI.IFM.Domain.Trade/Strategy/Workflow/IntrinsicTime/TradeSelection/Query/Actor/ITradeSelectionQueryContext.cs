using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Actor;
public interface ITradeSelectionQueryContext : IQueryActorContext<TradeSelectionQueryActor>
{
    IDbContextFactory DbFactory { get; }

    IPortfolioQueryApi PortfolioQueries { get; }

    ILogger<TradeSelectionQueryActor> Logger { get; }
}
