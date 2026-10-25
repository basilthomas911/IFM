using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.MarketData.Contracts;
namespace TomasAI.IFM.Application.MarketData.Host.OptionChains;
internal sealed class HostOptionChainContext(IDbContextFactory databases, IMarketDataApi api,
    QualifiedCompositionDiscovery discovery, ICompositionMarketDataApi market, DatasetWorkerAdmissionRegistry admissions,
    TreasuryPublicationPolicy publication, TreasuryRateConversionPolicy conversion, ILogger<HostOptionChainContext> logger)
{
    public IDbContextFactory DbFactory => databases;
    public IMarketDataApi MarketDataApi => api;
    public QualifiedCompositionDiscovery CompositionDiscovery => discovery;
    public ICompositionMarketDataApi CompositionMarketData => market;
    public DatasetWorkerAdmissionRegistry WorkerAdmissions => admissions;
    public TreasuryPublicationPolicy TreasuryPublication => publication;
    public TreasuryRateConversionPolicy TreasuryConversion => conversion;
    public ILogger Logger => logger;
    public DateOnly ValueDate => api.GetRuntimeStatus().ActiveValueDate
        ?? TomasAI.IFM.Domain.MarketData.Shared.FuturesTradingValueDate.GetOperational(DateTimeOffset.UtcNow);
}
