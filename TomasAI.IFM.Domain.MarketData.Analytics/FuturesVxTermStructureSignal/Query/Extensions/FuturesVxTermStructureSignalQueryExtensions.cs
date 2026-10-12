using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Query.Extensions;

/// <summary>Exposes readonly VX term-structure Query context properties.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class FuturesVxTermStructureSignalQueryExtensions
{
    extension(IQueryActorContext<FuturesVxTermStructureSignalQueryActor> context)
    {
        public IFuturesVxTermStructureSignalQueryContext DomainContext =>
            IsArgumentNull.Named(context as IFuturesVxTermStructureSignalQueryContext, nameof(context))!;
        public IDbContextFactory DbFactory => context.DomainContext.DbFactory;
        public ILogger<FuturesVxTermStructureSignalQueryActor> Logger => context.DomainContext.Logger;
    }
}
