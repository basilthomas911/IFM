using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command;

public static class RemoveFuturesContract
{
    /// <summary>
    /// Removes a futures contract from the specified command state if it exists.
    /// </summary>
    /// <param name="e">The remove command containing the details of the futures contract to be removed.</param>
    /// <param name="state">The current state of the futures contract commands to update.</param>
    /// <returns>true if the futures contract was successfully removed; otherwise, false.</returns>
    /// <exception cref="RemoveFuturesContractException">Thrown if the specified futures contract does not exist in the current state.</exception>
    public static bool Execute(this RemoveFuturesContractCommand e, FuturesContractCommandState state)
        => e switch
        {
            _ when state.FuturesContractDoesNotExist(e.ContractId, e.Overwrite) => throw new RemoveFuturesContractException(e.FuturesContractDoesNotExistErrorMsg()),
            _ => state.Update(e.CreateFuturesContractRemovedEvent(), e)
        };

    /// <summary>
    /// Creates a <see cref="FuturesContractRemovedEvent"/> from a <see cref="RemoveFuturesContractCommand"/>.
    /// </summary>
    /// <param name="e">The source remove command containing entity identifiers and origin metadata.</param>
    /// <returns>A fully-populated removed event ready to be applied to actor state.</returns>
    internal static FuturesContractRemovedEvent CreateFuturesContractRemovedEvent(this RemoveFuturesContractCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesContractRemovedEvent.Actor, FuturesContractRemovedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            ContractId = e.ContractId,
            DeletedOn = e.OriginatedOn,
            DeletedBy = e.OriginatedBy
        };

    /// <summary>
    /// Returns the error message for an attempt to remove a futures contract that does not exist.
    /// </summary>
    /// <param name="e">The remove command that triggered the missing-contract error.</param>
    /// <returns>A descriptive error message string.</returns>
    internal static string FuturesContractDoesNotExistErrorMsg(this RemoveFuturesContractCommand e)
        => $"{e.CommandName}: contract {e.ContractId} does not exist";
}
