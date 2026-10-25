using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

using TomasAI.IFM.Application.MarketData.Contracts;

using System.Reflection;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Databento;
namespace TomasAI.IFM.Application.MarketData.Remote;

/// <summary>Preserves the public API while forwarding asynchronous backend work through NATS.</summary>
public sealed partial class MarketDataReadMirror(IOptionChainCache optionChainCache) : IMarketDataApi
{
    public IOptionChainCache OptionChainCache => optionChainCache;
    public bool CoreFuturesRoutesAreRuntimeOwned => true;
    public bool TryGetMarketInstrumentId(string contractId, out uint marketInstrumentId) => MissingInstrument(out marketInstrumentId);
    public FuturesMarketHealthSnapshot GetFuturesMarketHealth(string contractId) => values.GetFuturesMarketHealth(contractId);
    public Task<TomasAI.IFM.Shared.EventSourcing.ServiceResult<TradeStrategySymbolReadModel[]>> GetTradeStrategySymbolsAsync(TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyType family,
        CancellationToken cancellationToken = default) => CallAsync<TomasAI.IFM.Shared.EventSourcing.ServiceResult<TradeStrategySymbolReadModel[]>>(nameof(GetTradeStrategySymbolsAsync), [typeof(TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyType), typeof(CancellationToken)], [family, null], cancellationToken);
    public Task<SubscriptionLeaseResult> StartStreamingFuturesTickDataAsync(SubscriptionAcquireRequest request, CancellationToken cancellationToken = default) => CallAsync<SubscriptionLeaseResult>(nameof(StartStreamingFuturesTickDataAsync), [typeof(SubscriptionAcquireRequest), typeof(CancellationToken)], [request, null], cancellationToken);
    public Task<bool> StartStreamingFuturesTickDataAsync(string futuresContractId,
        TickerStreamOwner? owner = null) => CallAsync<bool>(nameof(StartStreamingFuturesTickDataAsync), [typeof(string), typeof(TickerStreamOwner?)], [futuresContractId, owner], CancellationToken.None);
    public Task<SubscriptionLeaseResult> StartStreamingFuturesOptionTickDataAsync(SubscriptionAcquireRequest request, CancellationToken cancellationToken = default) => CallAsync<SubscriptionLeaseResult>(nameof(StartStreamingFuturesOptionTickDataAsync), [typeof(SubscriptionAcquireRequest), typeof(CancellationToken)], [request, null], cancellationToken);
    public Task<bool> StartStreamingFuturesOptionTickDataAsync(string futuresOptionContractId,
        TickerStreamOwner? owner = null) => CallAsync<bool>(nameof(StartStreamingFuturesOptionTickDataAsync), [typeof(string), typeof(TickerStreamOwner?)], [futuresOptionContractId, owner], CancellationToken.None);
    public Task<SubscriptionLeaseResult> StartStreamingFuturesOptionChainDataAsync(SubscriptionAcquireRequest request, CancellationToken cancellationToken = default) => CallAsync<SubscriptionLeaseResult>(nameof(StartStreamingFuturesOptionChainDataAsync), [typeof(SubscriptionAcquireRequest), typeof(CancellationToken)], [request, null], cancellationToken);
    public Task<SubscriptionLeaseResult> RenewSubscriptionLeaseAsync(SubscriptionRenewRequest request, CancellationToken cancellationToken = default) => CallAsync<SubscriptionLeaseResult>(nameof(RenewSubscriptionLeaseAsync), [typeof(SubscriptionRenewRequest), typeof(CancellationToken)], [request, null], cancellationToken);
    public Task<SubscriptionLeaseResult> ReleaseSubscriptionLeaseAsync(SubscriptionReleaseRequest request, CancellationToken cancellationToken = default) => CallAsync<SubscriptionLeaseResult>(nameof(ReleaseSubscriptionLeaseAsync), [typeof(SubscriptionReleaseRequest), typeof(CancellationToken)], [request, null], cancellationToken);
    public Task<SubscriptionLeaseResult> AcquireSelectedSubscriptionLeasesAsync(SubscriptionAcquireBatchRequest request, CancellationToken cancellationToken = default) => CallAsync<SubscriptionLeaseResult>(nameof(AcquireSelectedSubscriptionLeasesAsync), [typeof(SubscriptionAcquireBatchRequest), typeof(CancellationToken)], [request, null], cancellationToken);
    public Task<SubscriptionLeaseResult> GetSubscriptionLeasesAsync(SubscriptionOwnerQuery request, CancellationToken cancellationToken = default) => CallAsync<SubscriptionLeaseResult>(nameof(GetSubscriptionLeasesAsync), [typeof(SubscriptionOwnerQuery), typeof(CancellationToken)], [request, null], cancellationToken);
    public bool IsDatabentoFeedUp(TimeSpan? timeout = null) => values.IsFeedUp;
    public MarketDataFeedRuntimeStatusReadModel GetRuntimeStatus() => RuntimeStatus();
    public bool TryGetOnTheRunFuturesContract(string symbol,
        out FuturesContractV3ReadModel contract) => registry.TryGetOnTheRunFuturesContract(symbol, out contract);
    public bool TryGetFuturesTermStructureContracts(string symbol,
        out FuturesTermStructureContracts contracts) => registry.TryGetFuturesTermStructureContracts(symbol, out contracts);
    public Task<bool> UpdateFuturesTermStructureContractsAsync(string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken = default) => CallAsync<bool>(nameof(UpdateFuturesTermStructureContractsAsync), [typeof(string), typeof(DateOnly), typeof(CancellationToken)], [symbol, valueDate, null], cancellationToken);
    public Task<bool> UpdateOnTheRunFuturesContractAsync(string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken = default,
        bool forceProviderRefresh = false) => CallAsync<bool>(nameof(UpdateOnTheRunFuturesContractAsync), [typeof(string), typeof(DateOnly), typeof(CancellationToken), typeof(bool)], [symbol, valueDate, null, forceProviderRefresh], cancellationToken);
    public bool TryGetLastTickPrice(string contractId,
        out FuturesMarketPriceSnapshot snapshot) => values.TryGetLastTickPrice(contractId, out snapshot);
    public bool TryGetLastOptionTickPrice(string contractId,
        out OptionTickerPriceSnapshot snapshot) => MissingOption(out snapshot);
    public bool TryGetFuturesSessionStatistics(string contractId,
        out FuturesSessionStatisticsSnapshot snapshot) => values.TryGetFuturesSessionStatistics(contractId, out snapshot);
    public bool IsTickDataStreamActive(string contractId) => values.IsTickDataStreamActive(contractId);
    public async Task StartAsync(DateOnly valueDate,
        Func<Guid, int, string, Task>? errorMessageHandler = null,
        CancellationToken cancellationToken = default)
    {
        try { await CallAsync(nameof(StartAsync), [typeof(DateOnly), typeof(Func<Guid, int, string, Task>), typeof(CancellationToken)], [valueDate, null, null], cancellationToken).ConfigureAwait(false); }
        catch (Exception error)
        {
            if (errorMessageHandler is not null)
            {
                try { await errorMessageHandler(Guid.NewGuid(), 503, error.Message)
                    .WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false); }
                catch (Exception) { /* Preserve the original backend failure; an observer cannot hold recovery open. */ }
            }
            throw;
        }
    }
    public Task StopAsync(DateOnly valueDate) => CallAsync(nameof(StopAsync), [typeof(DateOnly)], [valueDate], CancellationToken.None);
    public Task<FuturesContractV3ReadModel?> GetFuturesContractAsync(string futuresContractId) => CallAsync<FuturesContractV3ReadModel?>(nameof(GetFuturesContractAsync), [typeof(string)], [futuresContractId], CancellationToken.None);
    public Task<FuturesContractV3ReadModel[]> GetFuturesContractsAsync(string[] futuresContractIds) => CallAsync<FuturesContractV3ReadModel[]>(nameof(GetFuturesContractsAsync), [typeof(string[])], [futuresContractIds], CancellationToken.None);
    public Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(string futuresOptionContractId) => CallAsync<FuturesOptionContractReadModel?>(nameof(GetFuturesOptionContractAsync), [typeof(string)], [futuresOptionContractId], CancellationToken.None);
    public Task<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(string[] futuresOptionContractIds) => CallAsync<FuturesOptionContractReadModel[]>(nameof(GetFuturesOptionContractsAsync), [typeof(string[])], [futuresOptionContractIds], CancellationToken.None);
    public Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsAsync(string futuresContractId,
        DateOnly maturityDate) => CallAsync<FuturesOptionContractReadModel[]>(nameof(GetFuturesOptionChainContractsAsync), [typeof(string), typeof(DateOnly)], [futuresContractId, maturityDate], CancellationToken.None);
    public Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsBySymbolAsync(string underlyingSymbol,
        DateOnly fromMaturityDate,
        DateOnly throughMaturityDate) => CallAsync<FuturesOptionContractReadModel[]>(nameof(GetFuturesOptionChainContractsBySymbolAsync), [typeof(string), typeof(DateOnly), typeof(DateOnly)], [underlyingSymbol, fromMaturityDate, throughMaturityDate], CancellationToken.None);
    public Task<OptionContractExpiryReadModel[]> DiscoverOptionContractExpiriesAsync(string underlyingSymbol,
        DateOnly fromExpiry,
        DateOnly throughExpiry,
        CancellationToken cancellationToken = default) => CallAsync<OptionContractExpiryReadModel[]>(nameof(DiscoverOptionContractExpiriesAsync), [typeof(string), typeof(DateOnly), typeof(DateOnly), typeof(CancellationToken)], [underlyingSymbol, fromExpiry, throughExpiry, null], cancellationToken);
    public Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsByRootAsync(string underlyingSymbol,
        string providerRoot,
        DateOnly maturityDate) => CallAsync<FuturesOptionContractReadModel[]>(nameof(GetFuturesOptionChainContractsByRootAsync), [typeof(string), typeof(string), typeof(DateOnly)], [underlyingSymbol, providerRoot, maturityDate], CancellationToken.None);
    public Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsByRootAsync(string underlyingSymbol, string providerRoot, DateOnly fromMaturityDate,
        DateOnly throughMaturityDate, CancellationToken cancellationToken = default) => CallAsync<FuturesOptionContractReadModel[]>(nameof(GetFuturesOptionChainContractsByRootAsync), [typeof(string), typeof(string), typeof(DateOnly), typeof(DateOnly), typeof(CancellationToken)], [underlyingSymbol, providerRoot, fromMaturityDate, throughMaturityDate, null], cancellationToken);
    public Task<decimal?> GetFuturesPriceAsync(string futuresContractId) => CallAsync<decimal?>(nameof(GetFuturesPriceAsync), [typeof(string)], [futuresContractId], CancellationToken.None);
    public Task<decimal?> GetFuturesOptionPriceAsync(string futuresOptionContractId) => CallAsync<decimal?>(nameof(GetFuturesOptionPriceAsync), [typeof(string)], [futuresOptionContractId], CancellationToken.None);
    public IFuturesLastPriceReader GetFuturesLastPriceReader(string futuresContractId) => values.GetFuturesReader(futuresContractId);
    public IFuturesOptionLastPriceReader GetFuturesOptionLastPriceReader(string futuresOptionContractId) => throw new NotSupportedException("Qualified option prices are supplied by backend risk snapshots.");
    public Task<bool> StopStreamingFuturesTickDataAsync(string futuresContractId,
        TickerStreamOwner? owner = null) => CallAsync<bool>(nameof(StopStreamingFuturesTickDataAsync), [typeof(string), typeof(TickerStreamOwner?)], [futuresContractId, owner], CancellationToken.None);
    public Task<bool> StopStreamingFuturesOptionTickDataAsync(string futuresOptionContractId,
        TickerStreamOwner? owner = null) => CallAsync<bool>(nameof(StopStreamingFuturesOptionTickDataAsync), [typeof(string), typeof(TickerStreamOwner?)], [futuresOptionContractId, owner], CancellationToken.None);
    async Task CallAsync(string operation, Type[] types, object?[] arguments, CancellationToken token)
        => _ = await CallAsync<bool>(operation, types, arguments, token).ConfigureAwait(false);
    Task<T> CallAsync<T>(string operation, Type[] types, object?[] arguments, CancellationToken token)
        => Task.FromException<T>(new NotSupportedException("Read mirrors perform no backend operations."));
}
