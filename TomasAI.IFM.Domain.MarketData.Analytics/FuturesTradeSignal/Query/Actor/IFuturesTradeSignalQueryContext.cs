using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Query.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesTradeSignalQueryActor"/>.</summary>
public interface IFuturesTradeSignalQueryContext : IQueryActorContext<FuturesTradeSignalQueryActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbFactory service supplied to the actor context.</summary>
    IDbContextFactory DbFactory { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesTradeSignalQueryActor> Logger { get; }
}
