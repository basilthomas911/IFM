using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command;

public static class RemoveFuturesOptionContract
{
    public static bool Execute(this RemoveFuturesOptionContractCommand e, FuturesOptionContractCommandState state)
        => e switch
        {
            _ when state.FuturesOptionContractDoesNotExist(e.ContractId, e.Overwrite) => throw new RemoveFuturesOptionContractException(e.FuturesOptionContractDoesNotExistErrorMsg()),
            _ => state.Update(e.CreateFuturesOptionContractRemovedEvent(), e)
        };

    /// <summary>
    /// Creates a <see cref="FuturesOptionContractRemovedEvent"/> from a <see cref="RemoveFuturesOptionContractCommand"/>.
    /// </summary>
    /// <param name="e">The source remove command containing entity identifiers and origin metadata.</param>
    /// <returns>A fully-populated removed event ready to be applied to actor state.</returns>
    internal static FuturesOptionContractRemovedEvent CreateFuturesOptionContractRemovedEvent(this RemoveFuturesOptionContractCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionContractRemovedEvent.Actor, FuturesOptionContractRemovedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            ContractId = e.ContractId,
            DeletedOn = e.OriginatedOn,
            DeletedBy = e.OriginatedBy
        };

    /// <summary>
    /// Returns the error message for an attempt to remove a futures option contract that does not exist.
    /// </summary>
    /// <param name="e">The remove command that triggered the missing-contract error.</param>
    /// <returns>A descriptive error message string.</returns>
    static string FuturesOptionContractDoesNotExistErrorMsg(this RemoveFuturesOptionContractCommand e)
        => $"{e.CommandName}: contractId {e.ContractId} does not exist";

}
