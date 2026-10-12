using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Query.Actor;

/// <summary>Defines readonly services required by the VWAP Query actor.</summary>
public interface IFuturesVwapSignalQueryContext : IQueryActorContext<FuturesVwapSignalQueryActor>
{
    IDbContextFactory DbFactory { get; }
    ILogger<FuturesVwapSignalQueryActor> Logger { get; }
}
