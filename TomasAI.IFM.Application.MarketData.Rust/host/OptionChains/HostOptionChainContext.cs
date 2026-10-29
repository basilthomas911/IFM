// Application qualification policy adapted from the current Host; native provider only.
﻿using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.MarketData.Contracts;
namespace TomasAI.IFM.Application.MarketData.Rust.Host.OptionChains;
internal sealed class HostOptionChainContext(IDbContextFactory databases, IMarketDataApi api,
    QualifiedCompositionDiscovery discovery, ICompositionMarketDataApi market, DatasetWorkerAdmissionRegistry admissions,
    TreasuryPublicationPolicy publication, TreasuryRateConversionPolicy conversion, ILogger<HostOptionChainContext> logger, TimeProvider clock)
{
    public IDbContextFactory DbFactory => databases;
    public IMarketDataApi MarketDataApi => api;
    public QualifiedCompositionDiscovery CompositionDiscovery => discovery;
    public ICompositionMarketDataApi CompositionMarketData => market;
    public DatasetWorkerAdmissionRegistry WorkerAdmissions => admissions;
    public TreasuryPublicationPolicy TreasuryPublication => publication;
    public TreasuryRateConversionPolicy TreasuryConversion => conversion;
    public ILogger Logger => logger;
    public TimeProvider Clock => clock;
    public DateOnly ValueDate => api.GetRuntimeStatus().ActiveValueDate
        ?? throw new InvalidOperationException("The API has no active trading ValueDate.");
}
