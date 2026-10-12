using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query.Actor;

/// <summary>Defines readonly services required by the VX term-structure Query actor.</summary>
public interface IFuturesVxTermStructureSignalQueryContext
    : IQueryActorContext<FuturesVxTermStructureSignalQueryActor>
{
    IDbContextFactory DbFactory { get; }
    ILogger<FuturesVxTermStructureSignalQueryActor> Logger { get; }
}
