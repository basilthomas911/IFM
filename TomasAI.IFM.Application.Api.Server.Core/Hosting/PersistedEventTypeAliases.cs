using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;

namespace TomasAI.IFM.Application.Api.Server.Core.Hosting;

/// <summary>Composition-owned read aliases. No stable-ID write cutover or event-contract move occurs here.</summary>
public static class PersistedEventTypeAliases
{
    public static void Register(EventTypeRegistry registry)
    {
        registry.Register<IronCondorPositionChangedEvent>("trade.iron-condor.position-changed/v1", EventWriteIdentityRegistry.Default.GetIdentity(typeof(IronCondorPositionChangedEvent)).EventTypeName);
        registry.Register<FuturesItiSignalGeneratedEvent>("market-data.futures.iti-signal-generated/v1", EventWriteIdentityRegistry.Default.GetIdentity(typeof(FuturesItiSignalGeneratedEvent)).EventTypeName);
        registry.Register<FundMandateCreatedEvent>("portfolio.fund-mandate-created/v1", EventWriteIdentityRegistry.Default.GetIdentity(typeof(FundMandateCreatedEvent)).EventTypeName);
    }
}
