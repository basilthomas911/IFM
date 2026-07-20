using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command;

public static class AddFuturesContract
{
    /// <summary>
    /// Attempts to add a new futures contract to the specified command state, optionally overwriting an existing
    /// contract if allowed.
    /// </summary>
    /// <param name="e">The command containing the details of the futures contract to add. Must not be null.</param>
    /// <param name="state">The current state to which the futures contract will be added. Must not be null.</param>
    /// <returns>true if the futures contract was successfully added; otherwise, false.</returns>
    /// <exception cref="AddFuturesContractException">Thrown if a futures contract with the same identifier already exists and overwriting is not permitted.</exception>
    public static bool Execute(this AddFuturesContractCommand e, FuturesContractCommandState state)
        => e switch
        {
            _ when state.FuturesContractExists(e.Contract.Id, e.Overwrite) => throw new AddFuturesContractException(e.FuturesContractExistsErrorMsg()),
            _ => state.Update(e.CreateFuturesContractAddedEvent(), e)
        };

    /// <summary>
    /// Creates a <see cref="FuturesContractAddedEvent"/> from an <see cref="AddFuturesContractCommand"/>.
    /// </summary>
    /// <param name="e">The source add command containing entity identifiers, payload, and origin metadata.</param>
    /// <returns>A fully-populated added event ready to be applied to actor state.</returns>
    internal static FuturesContractAddedEvent CreateFuturesContractAddedEvent(this AddFuturesContractCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesContractAddedEvent.Actor, FuturesContractAddedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            Contract = e.Contract,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };

    /// <summary>
    /// Returns the error message for an attempt to add a futures contract that already exists.
    /// </summary>
    /// <param name="e">The add command that triggered the duplicate-contract error.</param>
    /// <returns>A descriptive error message string.</returns>
    internal static string FuturesContractExistsErrorMsg(this AddFuturesContractCommand e)
        => $"{e.CommandName}: contract {e.Contract.ContractId} already exists";

}
