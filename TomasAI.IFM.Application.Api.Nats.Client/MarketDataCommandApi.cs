using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>
/// Market data API client for executing market data commands via HTTP.
/// </summary>
/// <param name="commandSvc">The command service API client.</param>
/// <exception cref="ArgumentNullException">Thrown when commandSvc is null.</exception>
public class MarketDataCommandApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer) , IMarketDataCommandApi
{
    /// <summary>
    /// Adds a new futures contract asynchronously.
    /// </summary>
    /// <param name="futuresContract">The futures contract data to be added. Cannot be <see langword="null"/>.</param>
    /// <param name="overwrite">A value indicating whether to overwrite an existing contract with the same identifier.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="ServiceResult{T}"/>
    /// with the unique identifier of the added futures contract.</returns>
    public async Task<ServiceResult<Guid>> AddFuturesContractAsync(FuturesContractV2ReadModel futuresContract, bool overwrite)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var cmd = new AddFuturesContractCommand(futuresContract, overwrite)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, AddFuturesContractCommand.Actor, AddFuturesContractCommand.Verb, futuresContract.Id.Format()),
                ErrorCode = AddFuturesContractCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, AddFuturesContractCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Updates an existing futures contract with the specified details.
    /// </summary>
    /// <param name="contractId">The unique identifier of the futures contract to update. Cannot be null or empty.</param>
    /// <param name="futuresContract">The updated details of the futures contract. Cannot be null.</param>
    /// <param name="overwrite">A value indicating whether to overwrite the existing contract details.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the updated futures contract.</returns>
    public async Task<ServiceResult<Guid>> ChangeFuturesContractAsync(FuturesContractId contractId, FuturesContractV2ReadModel futuresContract, bool overwrite)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var cmd = new ChangeFuturesContractCommand(contractId, futuresContract, overwrite)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ChangeFuturesContractCommand.Actor, ChangeFuturesContractCommand.Verb, (futuresContract.Id ?? contractId).Format()),
                ErrorCode = ChangeFuturesContractCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ChangeFuturesContractCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Removes a futures contract asynchronously based on the specified contract identifier.
    /// </summary>
    /// <param name="contractId">The unique identifier of the futures contract to be removed. Cannot be null.</param>
    /// <param name="overwrite">A boolean value indicating whether to overwrite any existing data associated with the contract.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the removed futures contract if the
    /// operation is successful.</returns>
    public async Task<ServiceResult<Guid>> RemoveFuturesContractAsync(FuturesContractId contractId, bool overwrite)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var cmd = new RemoveFuturesContractCommand(contractId, overwrite)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, RemoveFuturesContractCommand.Actor, RemoveFuturesContractCommand.Verb, contractId.Format()),
                ErrorCode = RemoveFuturesContractCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, RemoveFuturesContractCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Adds a new futures option contract asynchronously.
    /// </summary>
    /// <param name="futuresOptionContract">The view model representing the futures option contract to be added.</param>
    /// <param name="overwrite">A value indicating whether to overwrite an existing contract with the same identifier.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the added futures option contract.</returns>
    public async Task<ServiceResult<Guid>> AddFuturesOptionContractAsync(FuturesOptionContractReadModel futuresOptionContract, bool overwrite)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesOptionContractEntityId(futuresOptionContract.ContractId, futuresOptionContract.ContractMonth.Year);
            var cmd = new AddFuturesOptionContractCommand(futuresOptionContract, overwrite)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, AddFuturesOptionContractCommand.Actor, AddFuturesOptionContractCommand.Verb, entityId.Format()),
                ErrorCode = AddFuturesOptionContractCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, AddFuturesOptionContractCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Adds multiple futures option contracts asynchronously.
    /// </summary>
    /// <param name="futuresOptionContracts">The array of futures option contracts to add.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the operation.</returns>
    public async Task<ServiceResult<Guid>> AddFuturesOptionContractsAsync(int year, FuturesOptionContractReadModel[] futuresOptionContracts)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesOptionContractsEntityId(year);
            var cmd = new AddFuturesOptionContractsCommand(futuresOptionContracts)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, AddFuturesOptionContractsCommand.Actor, AddFuturesOptionContractsCommand.Verb, entityId.Format()),
                ErrorCode = AddFuturesOptionContractsCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, AddFuturesOptionContractsCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Updates an existing futures option contract.
    /// </summary>
    /// <param name="contractId">The contract identifier string.</param>
    /// <param name="futuresOptionContract">The updated contract details.</param>
    /// <param name="overwrite">Whether to overwrite existing data.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the operation.</returns>
    public async Task<ServiceResult<Guid>> ChangeFuturesOptionContractAsync(string contractId, FuturesOptionContractReadModel futuresOptionContract, bool overwrite)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new FuturesOptionContractEntityId(contractId, futuresOptionContract.ContractMonth.Year);
            var cmd = new ChangeFuturesOptionContractCommand(contractId, futuresOptionContract, overwrite)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ChangeFuturesOptionContractCommand.Actor, ChangeFuturesOptionContractCommand.Verb, entityId.Format()),
                ErrorCode = ChangeFuturesOptionContractCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ChangeFuturesOptionContractCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Removes a futures option contract.
    /// </summary>
    /// <param name="contractId">The contract identifier string.</param>
    /// <param name="overwrite">Whether to overwrite existing data.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the operation.</returns>
    public async Task<ServiceResult<Guid>> RemoveFuturesOptionContractAsync(string contractId, bool overwrite)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var futuresOptionContractId = new FuturesOptionContractId(contractId);
            var entityId = new FuturesOptionContractEntityId(contractId, futuresOptionContractId.MaturityDate.Year);
            var cmd = new RemoveFuturesOptionContractCommand(contractId, overwrite)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, RemoveFuturesOptionContractCommand.Actor, RemoveFuturesOptionContractCommand.Verb, entityId.Format()),
                ErrorCode = RemoveFuturesOptionContractCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, RemoveFuturesOptionContractCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Adds a yield curve rate.
    /// </summary>
    /// <param name="yieldCurveRate">The yield curve rate details.</param>
    /// <param name="overwrite">Whether to overwrite existing data.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the operation.</returns>
    public async Task<ServiceResult<Guid>> AddYieldCurveRateAsync(YieldCurveRateReadModel yieldCurveRate, bool overwrite)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new YieldCurveRateEntityId(yieldCurveRate.ValueDate.Year);
            var cmd = new AddYieldCurveRateCommand(yieldCurveRate, overwrite)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, AddYieldCurveRateCommand.Actor, AddYieldCurveRateCommand.Verb, entityId.Format()),
                ErrorCode = AddYieldCurveRateCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, AddYieldCurveRateCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Updates an existing yield curve rate.
    /// </summary>
    /// <param name="yieldCurveRate">The yield curve rate details.</param>
    /// <param name="overwrite">Whether to overwrite existing data.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the operation.</returns>
    public async Task<ServiceResult<Guid>> ChangeYieldCurveRateAsync(YieldCurveRateReadModel yieldCurveRate, bool overwrite)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new YieldCurveRateEntityId(yieldCurveRate.ValueDate.Year);
            var cmd = new ChangeYieldCurveRateCommand(yieldCurveRate, overwrite)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ChangeYieldCurveRateCommand.Actor, ChangeYieldCurveRateCommand.Verb, entityId.Format()),
                ErrorCode = ChangeYieldCurveRateCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ChangeYieldCurveRateCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Removes a yield curve rate.
    /// </summary>
    /// <param name="valueDate">The value date of the yield curve rate to remove.</param>
    /// <param name="overwrite">Whether to overwrite existing data.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the operation.</returns>
    public async Task<ServiceResult<Guid>> RemoveYieldCurveRateAsync(DateOnly valueDate, bool overwrite)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new YieldCurveRateEntityId(valueDate.Year);
            var cmd = new RemoveYieldCurveRateCommand(valueDate, overwrite)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, RemoveYieldCurveRateCommand.Actor, RemoveYieldCurveRateCommand.Verb, entityId.Format()),
                ErrorCode = RemoveYieldCurveRateCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, RemoveYieldCurveRateCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// Imports yield curve rates.
    /// </summary>
    /// <param name="importDate">The import date.</param>
    /// <param name="yieldCurveRates">The array of yield curve rates to import.</param>
    /// <returns>A <see cref="ServiceResult{T}"/> containing the unique identifier of the operation.</returns>
    public async Task<ServiceResult<Guid>> ImportYieldCurveRatesAsync(DateTime importDate, YieldCurveRateReadModel[] yieldCurveRates)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var entityId = new YieldCurveRateEntityId(importDate.Year);
            var cmd = new ImportYieldCurveRatesCommand(importDate, yieldCurveRates)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ImportYieldCurveRatesCommand.Actor, ImportYieldCurveRatesCommand.Verb, entityId.Format()),
                ErrorCode = ImportYieldCurveRatesCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd!, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ImportYieldCurveRatesCommand.ErrorId);
        }
        return serviceResult;
    }
}
