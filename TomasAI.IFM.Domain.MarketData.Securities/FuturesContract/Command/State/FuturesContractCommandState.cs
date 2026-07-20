using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;

/// <summary>
/// Represents the state of futures contracts within the system, including their creation, modification, and removal.
/// </summary>
/// <remarks>This class manages the lifecycle of futures contracts by applying domain events that represent state
/// changes. It maintains an internal mapping of futures contracts, allowing for efficient operations such as checking
/// for existence, adding, updating, and removing contracts. The state is updated based on specific domain events, such
/// as <see cref="FuturesContractAddedEvent"/>, <see cref="FuturesContractChangedEvent"/>, and <see
/// cref="FuturesContractRemovedEvent"/>.</remarks>
public class FuturesContractCommandState 
    :  BaseEventSourceActorState<FuturesContractCommandState>, IEventSourceActorState<FuturesContractCommandState>
{
    readonly FuturesContractStateModel _model = new();

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
                FuturesContractAddedEvent e => On(e),
                FuturesContractChangedEvent e => On(e),
                FuturesContractRemovedEvent e => On(e),
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
    bool On(FuturesContractAddedEvent e)
    {
        if (_model.ContainsKey(e.Contract.Id))
            _model.Remove(e.Contract.Id);
        _model.Add(e.Contract.Id, new FuturesSecuritiesContract(e.Contract));
        return true;
    }

    /// <summary>
    /// change futures contract
    /// </summary>
    /// <param name="e"></param>
    bool On(FuturesContractChangedEvent e)
    {
        if (_model.ContainsKey(e.OriginalContractId))
            _model.Remove(e.OriginalContractId);
        _model.Add(e.Contract.Id, new FuturesSecuritiesContract(e.Contract));
        return true;
    }

    /// <summary>
    /// delete futures contact
    /// </summary>
    /// <param name="e"></param>
    bool On(FuturesContractRemovedEvent e)
    {
        if (_model.ContainsKey(e.ContractId))
            _model.Remove(e.ContractId);
        return true;
    }

    /// <summary>
    /// Determines whether a futures contract with the specified identifier exists in the current state.
    /// </summary>
    /// <param name="futuresContractId">The unique identifier of the futures contract to check.</param>
    /// <returns>true if the futures contract exists in the state; otherwise, false.</returns>
    public bool FuturesContractExists(FuturesContractId futuresContractId)
        => _model.ContainsKey(futuresContractId);

    /// <summary>
    /// Determines whether a futures contract with the specified identifier exists in the current context.
    /// </summary>
    /// <param name="futuresContractId">The identifier of the futures contract to check for existence.</param>
    /// <param name="overwrite">A boolean value indicating whether to consider the contract as existing regardless of its presence in the map.
    /// If <see langword="true"/>, the method will return <see langword="true"/> even if the contract is not found.</param>
    /// <returns><see langword="true"/> if the futures contract exists in the map or if <paramref name="overwrite"/> is <see
    /// langword="true"/>;  otherwise, <see langword="false"/>.</returns>
    internal bool FuturesContractExists(FuturesContractId futuresContractId, bool overwrite)
        => _model.ContainsKey(futuresContractId) && !overwrite;

    /// <summary>
    /// Determines whether a futures contract with the specified identifier does not exist.
    /// </summary>
    /// <param name="futuresContractId">The unique identifier of the futures contract to check. Cannot be null or empty.</param>
    /// <param name="overwrite">A boolean value indicating whether the existence of the contract should be ignored.  If <see langword="true"/>,
    /// the method will return <see langword="false"/> regardless of the contract's existence.</param>
    /// <returns><see langword="true"/> if the futures contract does not exist and <paramref name="overwrite"/> is <see
    /// langword="false"/>;  otherwise, <see langword="false"/>.</returns>
    internal bool FuturesContractDoesNotExist(FuturesContractId futuresContractId, bool overwrite)
        => !_model.ContainsKey(futuresContractId) && !overwrite ;

}

