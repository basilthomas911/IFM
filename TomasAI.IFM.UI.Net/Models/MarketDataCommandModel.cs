using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.UI.Net.Models;

/// <summary>
/// Represents a model for executing market data commands, such as managing futures contracts, futures option contracts,
/// and yield curve rates.
/// </summary>
/// <remarks>This class provides methods to perform various operations on market data, including adding, updating,
/// and removing futures contracts, futures option contracts, and yield curve rates. Each operation is executed
/// asynchronously and interacts with the underlying <see cref="IMarketDataCommandApi"/> implementation. Callbacks are
/// provided to track the unique command identifiers for auditing or further processing.</remarks>
/// <param name="commandApi"></param>
public class MarketDataCommandModel(IMarketDataCommandApi commandApi) 
    : BaseModel<MarketDataCommandModel>
{
    readonly IMarketDataCommandApi _commandApi = IsArgumentNull.Set(commandApi);

    /// <summary>
    /// add futures contract
    /// </summary>
    /// <param name="futuresContract"></param>
    /// <param name="overwrite"></param>
    /// <param name="setCommandId"></param>
    public async Task AddFuturesContractAsync(FuturesContractV2ReadModel futuresContract, bool overwrite)
        => await ExecuteCommandAsync(() => _commandApi.AddFuturesContractAsync(futuresContract, overwrite));

    /// <summary>
    /// Updates an existing futures contract with new details.
    /// </summary>
    /// <remarks>This method sends a command to update the specified futures contract. The update can either
    /// overwrite  the existing contract details or apply changes without overwriting, based on the value of the 
    /// <paramref name="overwrite"/> parameter. The <paramref name="setCommandId"/> callback can be used to  track the
    /// command's unique identifier for further processing or auditing.</remarks>
    /// <param name="originalContractId">The unique identifier of the original futures contract to be updated. Cannot be null or empty.</param>
    /// <param name="changedFuturesContract">The updated details of the futures contract. Cannot be null.</param>
    /// <param name="overwrite">A value indicating whether the update should overwrite the existing contract details.  <see langword="true"/> to
    /// overwrite; otherwise, <see langword="false"/>.</param>
    /// <param name="setCommandId">A callback action that receives the unique identifier of the command executed to perform the update.  This
    /// action is invoked after the command is successfully initiated.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ChangeFuturesContractAsync(FuturesContractId originalContractId, FuturesContractV2ReadModel changedFuturesContract, bool overwrite)
        => await ExecuteCommandAsync(() => _commandApi.ChangeFuturesContractAsync(originalContractId, changedFuturesContract, overwrite));

    /// <summary>
    /// Removes a futures contract asynchronously.
    /// </summary>
    /// <param name="contractId">The unique identifier of the futures contract to be removed.</param>
    /// <param name="overwrite">A boolean value indicating whether to overwrite the existing contract if it already exists. <see
    /// langword="true"/> to overwrite the contract; otherwise, <see langword="false"/>.</param>
    /// <param name="setCommandId">A callback action that receives the unique identifier of the command executed to remove the contract. This can
    /// be used to track or log the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task RemoveFuturesContractAsync(FuturesContractId contractId, bool overwrite)
        => await ExecuteCommandAsync(() => _commandApi.RemoveFuturesContractAsync(contractId, overwrite));

    /// <summary>
    /// Adds a new futures option contract asynchronously.
    /// </summary>
    /// <remarks>This method executes the command to add the specified futures option contract. If <paramref
    /// name="overwrite"/> is <see langword="true"/>, any existing contract with the same identifier will be
    /// replaced.</remarks>
    /// <param name="futuresOptionContract">The futures option contract to be added. This parameter cannot be <see langword="null"/>.</param>
    /// <param name="overwrite">A value indicating whether to overwrite an existing contract with the same identifier. <see langword="true"/> to
    /// overwrite; otherwise, <see langword="false"/>.</param>
    /// <param name="setCommandId">A callback action that receives the unique identifier of the command associated with this operation. This
    /// parameter cannot be <see langword="null"/>.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task AddFuturesOptionContractAsync(FuturesOptionContractReadModel futuresOptionContract, bool overwrite)
        => await ExecuteCommandAsync(() => _commandApi.AddFuturesOptionContractAsync(futuresOptionContract, overwrite));

    /// <summary>
    /// add futures option contracts
    /// </summary>
    /// <param name="futuresOptionContracts"></param>
    /// <param name="onCompleted"></param>
    public async Task AddFuturesOptionContractsAsync(int year, FuturesOptionContractReadModel[] futuresOptionContracts, Action onCompleted)
        => await ExecuteCommandAsync(() => _commandApi.AddFuturesOptionContractsAsync(year, futuresOptionContracts), onCompleted);

    /// <summary>
    /// change futures option contract
    /// </summary>
    /// <param name="originalContractId"></param>
    /// <param name="changedFuturesOptionContract"></param>
    /// <param name="overwrite"></param>
    /// <param name="setCommandId"></param>
    public async Task ChangeFuturesOptionContractAsync(string originalContractId, FuturesOptionContractReadModel changedFuturesOptionContract,  bool overwrite)
        => await ExecuteCommandAsync(() => _commandApi.ChangeFuturesOptionContractAsync(originalContractId, changedFuturesOptionContract, overwrite));

    /// <summary>
    /// remove futures option contract
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="overwrite"></param>
    /// <param name="setCommandId"></param>
    public async Task RemoveFuturesOptionContractAsync(string contractId, bool overwrite)
        => await ExecuteCommandAsync(() => _commandApi.RemoveFuturesOptionContractAsync(contractId,  overwrite));

    /// <summary>
    /// add yield curve rate
    /// </summary>
    /// <param name="yieldCurveRate"></param>
    /// <param name="overwrite">Indicates whether to overwrite an existing yield curve rate with the same value date.</param>
    /// <param name="setCommandId"></param>
    public async Task AddYieldCurveRateAsync(YieldCurveRateReadModel yieldCurveRate, bool overwrite)
        => await ExecuteCommandAsync(() => _commandApi.AddYieldCurveRateAsync(yieldCurveRate, overwrite));

    /// <summary>
    /// change yield curve rate
    /// </summary>
    /// <param name="yieldCurveRate"></param>
    /// <param name="overwrite">Indicates whether to overwrite an existing yield curve rate with the same value date.</param>
    /// <param name="setCommandId"></param>
    public async Task ChangeYieldCurveRateAsync(YieldCurveRateReadModel yieldCurveRate, bool overwrite)
        => await ExecuteCommandAsync(() => _commandApi.ChangeYieldCurveRateAsync(yieldCurveRate, overwrite));

    /// <summary>
    /// remove yield curve rate
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="overwrite">Indicates whether to overwrite an existing yield curve rate with the same value date.</param>
    /// <param name="setCommandId"></param>
    public async Task RemoveYieldCurveRateAsync(DateOnly valueDate, bool overwrite)
        => await ExecuteCommandAsync(() => _commandApi.RemoveYieldCurveRateAsync(valueDate, overwrite));

    /// <summary>
    /// import yield curve rates
    /// </summary>
    /// <param name="importDate"></param>
    /// <param name="yieldCurveRates"></param>
    /// <param name="setCommandId"></param>
    public async Task ImportYieldCurveRatesAsync(DateTime importDate, YieldCurveRateReadModel[] yieldCurveRates)
        => await ExecuteCommandAsync(() => _commandApi.ImportYieldCurveRatesAsync(importDate, yieldCurveRates));

}
