using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.CommandParameters;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Api.Client;

/// <summary>
/// Market data API client for executing market data commands via HTTP.
/// </summary>
/// <param name="commandSvc">The command service API client.</param>
/// <exception cref="ArgumentNullException">Thrown when commandSvc is null.</exception>
public class MarketDataCommandApi(ICommandServiceApi commandSvc) : IMarketDataCommandApi
{
    readonly ICommandServiceApi _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// Adds a new futures contract asynchronously.
    /// </summary>
    /// <param name="futuresContract">The futures contract data to be added. Cannot be <see langword="null"/>.</param>
    /// <param name="overwrite">A value indicating whether to overwrite an existing contract with the same identifier.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="ServiceResult{T}"/>
    /// with the unique identifier of the added futures contract.</returns>
    public async Task<ServiceResult<Guid>> AddFuturesContractAsync(FuturesContractV2ReadModel futuresContract, bool overwrite)
        => await new AddFuturesContractParameter(IsArgumentNull.Set(futuresContract), overwrite, AddFuturesContractCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataUriPath.AddFuturesContract, e));

    /// <summary>
    /// Updates an existing futures contract with the specified details.
    /// </summary>
    /// <param name="contractId">The unique identifier of the futures contract to update. Cannot be null or empty.</param>
    /// <param name="futuresContract">The updated details of the futures contract. Cannot be null.</param>
    /// <param name="overwrite">A value indicating whether to overwrite the existing contract details.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the updated futures contract.</returns>
    public async Task<ServiceResult<Guid>> ChangeFuturesContractAsync(FuturesContractId contractId, FuturesContractV2ReadModel futuresContract, bool overwrite)
        => await new ChangeFuturesContractParameter(IsArgumentNull.Set(contractId), IsArgumentNull.Set(futuresContract), overwrite, ChangeFuturesContractCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataUriPath.ChangeFuturesContract, e));

    /// <summary>
    /// Removes a futures contract asynchronously based on the specified contract identifier.
    /// </summary>
    /// <param name="contractId">The unique identifier of the futures contract to be removed. Cannot be null.</param>
    /// <param name="overwrite">A boolean value indicating whether to overwrite any existing data associated with the contract.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the removed futures contract if the
    /// operation is successful.</returns>
    public async Task<ServiceResult<Guid>> RemoveFuturesContractAsync(FuturesContractId contractId, bool overwrite)
        => await new RemoveFuturesContractParameter(IsArgumentNull.Set(contractId), overwrite, RemoveFuturesContractCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataUriPath.RemoveFuturesContract, e));

    /// <summary>
    /// Adds a new futures option contract asynchronously.
    /// </summary>
    /// <param name="futuresOptionContract">The view model representing the futures option contract to be added.</param>
    /// <param name="overwrite">A value indicating whether to overwrite an existing contract with the same identifier.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the added futures option contract.</returns>
    public async Task<ServiceResult<Guid>> AddFuturesOptionContractAsync(FuturesOptionContractReadModel futuresOptionContract, bool overwrite)
        => await new AddFuturesOptionContractParameter(IsArgumentNull.Set(futuresOptionContract), futuresOptionContract.ContractMonth.Year, overwrite, AddFuturesOptionContractCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataUriPath.AddFuturesOptionContract, e));

    /// <summary>
    /// Adds multiple futures option contracts asynchronously.
    /// </summary>
    /// <param name="futuresOptionContracts">The array of futures option contracts to add.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the operation.</returns>
    public async Task<ServiceResult<Guid>> AddFuturesOptionContractsAsync(int year, FuturesOptionContractReadModel[] futuresOptionContracts)
        => await new AddFuturesOptionContractsParameter(year, IsArgumentNull.Set(futuresOptionContracts), AddFuturesOptionContractsCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataUriPath.AddFuturesOptionContracts, e));

    /// <summary>
    /// Updates an existing futures option contract.
    /// </summary>
    /// <param name="contractId">The contract identifier string.</param>
    /// <param name="futuresOptionContract">The updated contract details.</param>
    /// <param name="overwrite">Whether to overwrite existing data.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the operation.</returns>
    public async Task<ServiceResult<Guid>> ChangeFuturesOptionContractAsync(string contractId, FuturesOptionContractReadModel futuresOptionContract, bool overwrite)
        => await new ChangeFuturesOptionContractParameter(IsArgumentNull.Set(contractId), IsArgumentNull.Set(futuresOptionContract), overwrite, ChangeFuturesOptionContractCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataUriPath.ChangeFuturesOptionContract, e));

    /// <summary>
    /// Removes a futures option contract.
    /// </summary>
    /// <param name="contractId">The contract identifier string.</param>
    /// <param name="overwrite">Whether to overwrite existing data.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the operation.</returns>
    public async Task<ServiceResult<Guid>> RemoveFuturesOptionContractAsync(string contractId, bool overwrite)
        => await new RemoveFuturesOptionContractParameter(IsArgumentNull.Set(contractId), overwrite, RemoveFuturesOptionContractCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataUriPath.RemoveFuturesOptionContract, e));

    /// <summary>
    /// Adds a yield curve rate.
    /// </summary>
    /// <param name="yieldCurveRate">The yield curve rate details.</param>
    /// <param name="overwrite">Whether to overwrite existing data.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the operation.</returns>
    public async Task<ServiceResult<Guid>> AddYieldCurveRateAsync(YieldCurveRateReadModel yieldCurveRate, bool overwrite)
        => await new AddYieldCurveRateParameter(IsArgumentNull.Set(yieldCurveRate), overwrite, AddYieldCurveRateCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataUriPath.AddYieldCurveRate, e));

    /// <summary>
    /// Updates an existing yield curve rate.
    /// </summary>
    /// <param name="yieldCurveRate">The yield curve rate details.</param>
    /// <param name="overwrite">Whether to overwrite existing data.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the operation.</returns>
    public async Task<ServiceResult<Guid>> ChangeYieldCurveRateAsync(YieldCurveRateReadModel yieldCurveRate, bool overwrite)
        => await new ChangeYieldCurveRateParameter(IsArgumentNull.Set(yieldCurveRate), overwrite, ChangeYieldCurveRateCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataUriPath.ChangeYieldCurveRate, e));

    /// <summary>
    /// Removes a yield curve rate.
    /// </summary>
    /// <param name="valueDate">The value date of the yield curve rate to remove.</param>
    /// <param name="overwrite">Whether to overwrite existing data.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the operation.</returns>
    public async Task<ServiceResult<Guid>> RemoveYieldCurveRateAsync(DateOnly valueDate, bool overwrite)
        => await new RemoveYieldCurveRateParameter(valueDate, overwrite, RemoveYieldCurveRateCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataUriPath.RemoveYieldCurveRate, e));

    /// <summary>
    /// Imports yield curve rates.
    /// </summary>
    /// <param name="importDate">The import date.</param>
    /// <param name="yieldCurveRates">The array of yield curve rates to import.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the operation.</returns>
    public async Task<ServiceResult<Guid>> ImportYieldCurveRatesAsync(DateTime importDate, YieldCurveRateReadModel[] yieldCurveRates)
        => await new ImportYieldCurveRatesParameter(importDate, IsArgumentNull.Set(yieldCurveRates), ImportYieldCurveRatesCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(MarketDataUriPath.ImportYieldCurveRates, e));
}
