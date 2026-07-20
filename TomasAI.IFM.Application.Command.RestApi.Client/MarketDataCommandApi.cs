using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Framework.Messaging;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.Command.Client;

/// <summary>
/// market data api command constructor
/// </summary>
/// <param name="commandSvc"></param>
/// <exception cref="ArgumentNullException"></exception>
public class MarketDataCommandApi(ICommandService commandSvc) : IMarketDataCommandApi
{
    const string MarketDataController = "MarketData";
    readonly ICommandService _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// Adds a new futures contract asynchronously.
    /// </summary>
    /// <param name="futuresContract">The futures contract data to be added. Cannot be <see langword="null"/>.</param>
    /// <param name="overwrite">A value indicating whether to overwrite an existing contract with the same identifier.</param>
    /// <param name="setCommandId">An action to set the command identifier for the operation. The action is invoked with the generated command ID.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="ServiceResult{T}"/>
    /// with the unique identifier of the added futures contract.</returns>
    public async Task<ServiceResult<Guid>> AddFuturesContractAsync(FuturesContractV2ReadModel futuresContract, bool overwrite, Action<Guid> setCommandId)
        => await new AddFuturesContractCommand(IsArgumentNull.Set(futuresContract), overwrite)
            .SetCommandId(setCommandId)
            .SetSubject(AddFuturesContractCommand.Actor, AddFuturesContractCommand.Verb)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataController));

    /// <summary>
    /// Updates an existing futures contract with the specified details.
    /// </summary>
    /// <remarks>This method executes an asynchronous operation to update the specified futures contract.  The
    /// <paramref name="setCommandId"/> callback is invoked with the command ID to allow tracking of the
    /// operation.</remarks>
    /// <param name="contractId">The unique identifier of the futures contract to update. Cannot be null or empty.</param>
    /// <param name="futuresContract">The updated details of the futures contract. Cannot be null.</param>
    /// <param name="overwrite">A value indicating whether to overwrite the existing contract details.  <see langword="true"/> to overwrite;
    /// otherwise, <see langword="false"/>.</param>
    /// <param name="setCommandId">A callback action to set the command identifier for the operation.  The action is invoked with the generated
    /// command ID.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the updated futures contract.</returns>
    public async Task<ServiceResult<Guid>> ChangeFuturesContractAsync(FuturesContractId contractId, FuturesContractV2ReadModel futuresContract, bool overwrite, Action<Guid> setCommandId)
    {
        ChangeFuturesContractCommand command = new (contractId, futuresContract)
        {
            Overwrite = overwrite
        };
        return await command
            .SetCommandId(setCommandId)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataController));
    }

    /// <summary>
    /// Removes a futures contract asynchronously based on the specified contract identifier.
    /// </summary>
    /// <remarks>This method executes the removal operation asynchronously and allows the caller to specify
    /// whether to overwrite existing data. The <paramref name="setCommandId"/> action can be used to track the command
    /// ID for auditing or logging purposes.</remarks>
    /// <param name="contractId">The unique identifier of the futures contract to be removed. Cannot be null.</param>
    /// <param name="overwrite">A boolean value indicating whether to overwrite any existing data associated with the contract. <see
    /// langword="true"/> to overwrite; otherwise, <see langword="false"/>.</param>
    /// <param name="setCommandId">An action to set the command identifier for the operation. This action is invoked with the generated command ID.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the removed futures contract if the
    /// operation is successful.</returns>
    public async Task<ServiceResult<Guid>> RemoveFuturesContractAsync(FuturesContractId contractId, bool overwrite, Action<Guid> setCommandId)
        => await new RemoveFuturesContractCommand(IsArgumentNull.Set(contractId)) { Overwrite = overwrite }
        .SetCommandId(setCommandId)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataController));

    /// <summary>
    /// Adds a new futures option contract asynchronously.
    /// </summary>
    /// <remarks>This method executes the addition of a futures option contract using a command-based
    /// approach.  The <paramref name="setCommandId"/> action can be used to track the command's unique identifier  for
    /// further processing or auditing.</remarks>
    /// <param name="futuresOptionContract">The view model representing the futures option contract to be added.  This parameter cannot be <see
    /// langword="null"/>.</param>
    /// <param name="overwrite">A value indicating whether to overwrite an existing contract with the same identifier. If <see
    /// langword="true"/>, any existing contract with the same identifier will be replaced.</param>
    /// <param name="setCommandId">An action to set the unique identifier of the command being executed.  This action is invoked with the generated
    /// command ID.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the added futures option contract.</returns>
    public async Task<ServiceResult<Guid>> AddFuturesOptionContractAsync(FuturesOptionContractReadModel futuresOptionContract, bool overwrite, Action<Guid> setCommandId)
   => await new AddFuturesOptionContractCommand(IsArgumentNull.Set(futuresOptionContract)) { Overwrite = overwrite }
        .SetCommandId(setCommandId)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataController));
    

    /// <summary>
    /// add futures option contracts
    /// </summary>
    /// <param name="futuresOptionContracts"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddFuturesOptionContractsAsync(FuturesOptionContractReadModel[] futuresOptionContracts)
        => await new AddFuturesOptionContractsCommand(IsArgumentNull.Set(futuresOptionContracts))
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataController));

    /// <summary>
    /// change futures option contract
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="futuresOptionContract"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ChangeFuturesOptionContractAsync(string contractId, FuturesOptionContractReadModel futuresOptionContract, bool overwrite, Action<Guid> setCommandId)
        => await new ChangeFuturesOptionContractCommand(IsArgumentNull.Set(contractId), IsArgumentNull.Set(futuresOptionContract)) { Overwrite = overwrite }
            .SetCommandId(setCommandId)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataController));

    /// <summary>
    /// remove futures option contract
    /// </summary>
    /// <param name="contractId"></param>
    /// <param name="overwrite"></param>
    /// <param name="setCommandId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveFuturesOptionContractAsync(string contractId, bool overwrite, Action<Guid> setCommandId)
        => await new RemoveFuturesOptionContractCommand(IsArgumentNull.Set(contractId)) { Overwrite = overwrite }
            .SetCommandId(setCommandId)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataController));

    /// <summary>
    /// add yield curve rate
    /// </summary>
    /// <param name="yieldCurveRate"></param>
    /// <param name="overwrite"></param>
    /// <param name="setCommandId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddYieldCurveRateAsync(YieldCurveRateReadModel yieldCurveRate, bool overwrite, Action<Guid> setCommandId)
        => await new AddYieldCurveRateCommand(IsArgumentNull.Set(yieldCurveRate)) { Overwrite = overwrite }
            .SetCommandId(setCommandId)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataController));

    /// <summary>
    /// change yield curve rate
    /// </summary>
    /// <param name="yieldCurveRate"></param>
    /// <param name="overwrite"></param>
    /// <param name="setCommandId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ChangeYieldCurveRateAsync(YieldCurveRateReadModel yieldCurveRate, bool overwrite, Action<Guid> setCommandId)
        => await new ChangeYieldCurveRateCommand(IsArgumentNull.Set(yieldCurveRate)) { Overwrite = overwrite }
        .SetCommandId(setCommandId)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataController));

    /// <summary>
    /// remove yield curve rate
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="overwrite"></param>    
    /// <param name="setCommandId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveYieldCurveRateAsync(DateOnly valueDate, bool overwrite, Action<Guid> setCommandId)
        => await new RemoveYieldCurveRateCommand(valueDate) { Overwrite = overwrite }
        .SetCommandId(setCommandId)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataController));

    /// <summary>
    /// import yield curve rates
    /// </summary>
    /// <param name="importDate"></param>
    /// <param name="yieldCurveRates"></param>
    /// <param name="setCommandId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ImportYieldCurveRatesAsync(DateTime importDate, YieldCurveRateReadModel[] yieldCurveRates, Action<Guid> setCommandId)
        => await new ImportYieldCurveRatesCommand(importDate, IsArgumentNull.Set(yieldCurveRates))
        .SetCommandId(setCommandId)
        .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, MarketDataController));
}
