using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>
/// Defines the application boundary for querying futures and futures-option
/// contracts and prices and controlling their live market-data streams.
/// </summary>
/// <remarks>
/// Contract identifiers are canonical domain identifiers. Provider-specific
/// symbols, instruments, requests, and subscription models do not cross this
/// interface. Contract-definition queries, hot-price access, and live-stream
/// controls share this single application boundary; the provider implementation
/// may use multiple protocol-specific transports internally.
/// </remarks>
public interface IMarketDataApi
{
    /// <summary>
    /// Reads the startup-validated currently traded futures contract for a root symbol
    /// from the in-memory rollover registry.
    /// </summary>
    /// <param name="symbol">The futures root symbol, such as <c>ES</c> or <c>VX</c>.</param>
    /// <param name="contract">The current contract when the symbol is registered.</param>
    /// <returns><see langword="true"/> when a current contract is available.</returns>
    bool TryGetCurrentlyTradedFuturesContract(
        string symbol,
        out FuturesContractV2ReadModel contract);

    /// <summary>
    /// Resolves and persists the currently traded futures contract when the
    /// symbol's rollover configuration is incomplete or due.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the stored next-rollover date was first set
    /// or changed; otherwise <see langword="false"/>.
    /// </returns>
    Task<bool> UpdateCurrentlyTradedFuturesContractAsync(
        string symbol,
        DateOnly valueDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the latest normalized market-price hot-cache snapshot without checking stream ownership.
    /// </summary>
    bool TryGetLastTickPrice(
        string contractId,
        out FuturesMarketPriceSnapshot snapshot);

    /// <summary>
    /// Reads the latest futures-option hot-cache snapshot without checking stream ownership.
    /// </summary>
    bool TryGetLastOptionTickPrice(
        string contractId,
        out OptionTickerPriceSnapshot snapshot);

    /// <summary>
    /// Returns whether at least one workflow currently owns the contract's transient tick stream.
    /// This check is independent from the last-price cache.
    /// </summary>
    bool IsTickDataStreamActive(string contractId);

    Task StartAsync(
        DateOnly valueDate,
        Func<Guid, int, string, Task>? errorMessageHandler = null,
        CancellationToken cancellationToken = default);
    Task StopAsync(DateOnly valueDate);

    Task<FuturesContractV2ReadModel?> GetFuturesContractAsync(
        string futuresContractId);

    Task<FuturesContractV2ReadModel[]> GetFuturesContractsAsync(
        string[] futuresContractIds);

    Task<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
        string futuresOptionContractId);

    Task<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(
        string[] futuresOptionContractIds);

    /// <summary>
    /// Gets every currently available call and put definition for one futures
    /// option chain so the domain layer can apply its own filtering.
    /// </summary>
    /// <param name="futuresContractId">
    /// The canonical domain contract ID of the underlying futures contract.
    /// </param>
    /// <param name="maturityDate">The exact option-chain maturity date.</param>
    /// <returns>
    /// Domain futures-option contracts ordered by strike, option type, and
    /// contract ID; an empty array when no definitions exist.
    /// </returns>
    Task<FuturesOptionContractReadModel[]> GetFuturesOptionChainContractsAsync(
        string futuresContractId,
        DateOnly maturityDate);

    Task<decimal> GetFuturesPriceAsync(
        string futuresContractId);

    Task<decimal?> GetFuturesOptionPriceAsync(
        string futuresOptionContractId);

    /// <summary>
    /// Gets the epoch-bound hot-value reader for one domain futures contract.
    /// Getting a reader does not start a subscription.
    /// </summary>
    IFuturesLastPriceReader GetFuturesLastPriceReader(
        string futuresContractId);

    /// <summary>
    /// Gets the epoch-bound hot-value reader for one domain futures-option contract.
    /// Getting a reader does not start a subscription.
    /// </summary>
    IFuturesOptionLastPriceReader GetFuturesOptionLastPriceReader(
        string futuresOptionContractId);

    Task<bool> StartStreamingFuturesTickDataAsync(
        string futuresContractId,
        TickerStreamOwner? owner = null);

    Task<bool> StopStreamingFuturesTickDataAsync(
        string futuresContractId,
        TickerStreamOwner? owner = null);

    Task<bool> StartStreamingFuturesOptionTickDataAsync(
        string futuresOptionContractId,
        TickerStreamOwner? owner = null);

    Task<bool> StopStreamingFuturesOptionTickDataAsync(
        string futuresOptionContractId,
        TickerStreamOwner? owner = null);

    /// <summary>
    /// Starts one futures-option chain using domain contract identifiers.
    /// </summary>
    /// <param name="futuresContractId">
    /// The canonical domain contract ID of the underlying futures contract.
    /// </param>
    /// <param name="maturityDate">The exact option-chain maturity date.</param>
    /// <param name="optionContractIds">
    /// The canonical domain contract IDs of the futures options to include in the chain.
    /// All contracts must resolve to the supplied underlying contract and maturity.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when a new chain stream is started; otherwise,
    /// <see langword="false"/> when the identical chain is already running.
    /// </returns>
    Task<bool> StartStreamingFuturesOptionChainDataAsync(
        string futuresContractId,
        DateOnly maturityDate,
        string[] optionContractIds);

    /// <summary>
    /// Stops one futures-option chain identified by its domain underlying contract and maturity.
    /// </summary>
    /// <param name="futuresContractId">
    /// The canonical domain contract ID of the underlying futures contract.
    /// </param>
    /// <param name="maturityDate">The exact option-chain maturity date.</param>
    /// <returns>
    /// <see langword="true"/> when a running chain is stopped; otherwise,
    /// <see langword="false"/> when no matching chain is running.
    /// </returns>
    Task<bool> StopStreamingFuturesOptionChainDataAsync(
        string futuresContractId,
        DateOnly maturityDate);
}
