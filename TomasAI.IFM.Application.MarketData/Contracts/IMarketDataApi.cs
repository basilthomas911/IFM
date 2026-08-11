using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.LastPrice;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>
/// Defines the application boundary for querying futures and futures-option
/// contracts and prices and controlling their live market-data streams.
/// </summary>
/// <remarks>
/// Contract identifiers are canonical domain identifiers. Provider-specific
/// symbols, instruments, requests, and subscription models do not cross this
/// interface.
/// </remarks>
public interface IMarketDataApi
{
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
        string futuresContractId);

    Task<bool> StopStreamingFuturesTickDataAsync(
        string futuresContractId);

    Task<bool> StartStreamingFuturesOptionTickDataAsync(
        string futuresOptionContractId);

    Task<bool> StopStreamingFuturesOptionTickDataAsync(
        string futuresOptionContractId);

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

public interface IMarketDataSnapshotApi : IMarketDataApi
{

}
