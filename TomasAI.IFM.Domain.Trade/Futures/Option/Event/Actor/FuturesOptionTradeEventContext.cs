using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Event.Actor;
/// <summary>Provides the runtime context for the futures-option event actor.</summary>
public sealed class FuturesOptionTradeEventContext : EventActorContext, IFuturesOptionTradeEventContext
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "This typed context adapts services for its actor and intentionally retains the actor logger category.")]
    /// <summary>Initializes the futures-option event actor context.</summary>
    /// <param name = "supervisor">The actor supervisor.</param>
    /// <param name = "portfolioValuation">The Portfolio trade valuation boundary.</param>
    /// <param name = "statusConsoleWriter">The status console writer.</param>
    /// <param name = "logger">The actor logger.</param>
    public FuturesOptionTradeEventContext(IActorSupervisor supervisor, IPortfolioTradeValuationApi portfolioValuation, IStatusConsoleWriter statusConsoleWriter, ILogger<FuturesOptionTradeEventActor> logger) : base(supervisor, new ActorMailboxId(ActorType.Event, FuturesOptionTradeEventActor.ActorName))
    {
        PortfolioValuation = portfolioValuation;
        StatusConsoleWriter = statusConsoleWriter;
        Logger = logger;
    }

    /// <inheritdoc/>
    public IPortfolioTradeValuationApi PortfolioValuation { get; }
    /// <inheritdoc/>
    public IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <inheritdoc/>
    public ILogger<FuturesOptionTradeEventActor> Logger { get; }
}
