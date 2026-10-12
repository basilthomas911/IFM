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
/// <summary>Defines runtime services available to futures-option event handlers.</summary>
public interface IFuturesOptionTradeEventContext : IEventActorContext<FuturesOptionTradeEventActor>
{
    /// <summary>Gets the Portfolio-owned trade valuation boundary.</summary>
    IPortfolioTradeValuationApi PortfolioValuation { get; }

    /// <summary>Gets the status console writer.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }

    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesOptionTradeEventActor> Logger { get; }
}
