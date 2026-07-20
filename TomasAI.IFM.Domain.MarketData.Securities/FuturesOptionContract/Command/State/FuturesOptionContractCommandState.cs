using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;

/// <summary>
/// Represents the state of a futures option contract, including methods to handle state changes triggered by domain
/// events.
/// </summary>
/// <remarks>This class manages the internal state of futures option contracts by applying domain events such as
/// additions, updates, and removals. It provides mechanisms to determine the existence of contracts and ensures the
/// state remains consistent with the events processed.</remarks>
public class FuturesOptionContractCommandState
    :  BaseEventSourceActorState<FuturesOptionContractCommandState>, IEventSourceActorState<FuturesOptionContractCommandState>
{
    readonly FuturesOptionContractModel _model = new();

    public override ActorThreadId Id { get; set; }

    /// <summary>
    /// apply state change event
    /// </summary>
    /// <param name="domainEvent"></param>
    /// <returns></returns>
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                FuturesOptionContractAddedEvent e => On(e),
                FuturesOptionContractsAddedEvent e => On(e),
                FuturesOptionContractChangedEvent e => On(e),
                FuturesOptionContractRemovedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    /// <summary>
    /// create futures contract
    /// </summary>
    /// <param name="e"></param>
    bool On(FuturesOptionContractAddedEvent e)
    {
        if (_model.ContainsKey(e.Contract.Id.ContractId))
            _model.Remove(e.Contract.Id.ContractId);
        _model.Add(e.Contract.Id.ContractId, new FuturesOptionSecuritiesContract(e.Contract));
        return true;
    }

    /// <summary>
    /// Handles the addition of futures option contracts by updating the internal model.
    /// </summary>
    /// <remarks>This method processes each contract in the event, removing any existing entries with the same
    /// contract ID from the internal model before adding the new contracts. The internal model is updated to reflect
    /// the latest state of the contracts.</remarks>
    /// <param name="e">The event containing the futures option contracts to be added.</param>
    /// <returns><see langword="true"/> if the operation completes successfully.</returns>
    bool On(FuturesOptionContractsAddedEvent e)
    {
        foreach (var contract in e.Contracts)
        {
            if (_model.ContainsKey(contract.ContractId))
                _model.Remove(contract.ContractId);
            _model.Add(contract.ContractId, new FuturesOptionSecuritiesContract(contract));
        }
        return true;
    }

    /// <summary>
    /// Handles the event when a futures option contract is changed.
    /// </summary>
    /// <param name="e">The event containing details about the changed futures option contract, including the original and updated
    /// contract information.</param>
    /// <returns><see langword="true"/> to indicate the event was successfully processed.</returns>
    bool On(FuturesOptionContractChangedEvent e)
    {
        if (_model.ContainsKey(e.OriginalContractId))
            _model.Remove(e.OriginalContractId);
        _model.Add(e.Contract.Id.ContractId, new FuturesOptionSecuritiesContract(e.Contract));
        return true;
    }

    /// <summary>
    /// Handles the removal of a futures option contract from the model.
    /// </summary>
    /// <param name="e">The event containing the details of the futures option contract to be removed.</param>
    /// <returns><see langword="true"/> to indicate the operation was successfully processed.</returns>
    bool On(FuturesOptionContractRemovedEvent e)
    {
        if (_model.ContainsKey(e.ContractId))
            _model.Remove(e.ContractId);
        return true;
    }

    /// <summary>
    /// Determines whether a futures option contract with the specified identifier exists in the model.
    /// </summary>
    /// <param name="futuresOptionContractId">The unique identifier of the futures option contract to check.</param>
    /// <param name="overwrite">A value indicating whether the existence check should consider overwriting is allowed. If
    /// <see langword="true"/>, the method will return <see langword="true"/> regardless of the contract's existence.</param>
    /// <returns><see langword="true"/> if the futures option contract exists or overwriting is allowed; otherwise, <see
    /// langword="false"/>.</returns>
    internal bool FuturesOptionContractExists(string futuresOptionContractId, bool overwrite)
        => _model.ContainsKey(futuresOptionContractId) || overwrite;

    /// <summary>
    /// Determines whether a futures option contract does not exist in the current model.
    /// </summary>
    /// <param name="futuresOptionContractId">The unique identifier of the futures option contract to check.</param>
    /// <param name="overwrite">A value indicating whether to bypass the existence check. If <see langword="true"/>, the method always returns
    /// <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if the futures option contract does not exist in the model and <paramref
    /// name="overwrite"/> is <see langword="false"/>; otherwise, <see langword="false"/>.</returns>
    internal bool FuturesOptionContractDoesNotExist(string futuresOptionContractId, bool overwrite)
        => !FuturesOptionContractExists(futuresOptionContractId, overwrite);

}
