using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.MarketData.Exceptions;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command;

public static class ChangeFuturesContract
{
    public static bool Execute(this ChangeFuturesContractCommand e, FuturesContractCommandState state)
       => e switch
       {
           _ when state.FuturesContractDoesNotExist(e.ContractId, e.Overwrite) => throw new ChangeFuturesContractException(e.FuturesContractDoesNotExistErrorMsg()),
           _ => state.Update(e.CreateFuturesContractChangedEvent(), e)
       };

    /// <summary>
    /// Creates a <see cref="FuturesContractChangedEvent"/> from a <see cref="ChangeFuturesContractCommand"/>.
    /// </summary>
    /// <param name="e">The source change command containing entity identifiers, payload, and origin metadata.</param>
    /// <returns>A fully-populated changed event ready to be applied to actor state.</returns>
    internal static FuturesContractChangedEvent CreateFuturesContractChangedEvent(this ChangeFuturesContractCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesContractChangedEvent.Actor, FuturesContractChangedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            OriginalContractId = e.ContractId,
            Contract = e.Contract,
            UpdatedOn = e.OriginatedOn,
            UpdatedBy = e.OriginatedBy
        };

    /// <summary>
    /// Returns the error message for an attempt to change a futures contract that does not exist.
    /// </summary>
    /// <param name="e">The change command that triggered the missing-contract error.</param>
    /// <returns>A descriptive error message string.</returns>
    internal static string FuturesContractDoesNotExistErrorMsg(this ChangeFuturesContractCommand e)
        => $"{e.CommandName}: contract {e.ContractId} does not exist";

}
