using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command;

public static class AddFuturesOptionContract
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    /// <exception cref="AddFuturesOptionContractException"></exception>
    public static bool Execute(this AddFuturesOptionContractCommand e, FuturesOptionContractCommandState state)
        => e switch
        {
            _ when state.FuturesOptionContractExists(e.Contract.Id.ContractId, e.Overwrite) => throw new AddFuturesOptionContractException(e.FuturesOptionContractExistsErrorMsg()),
            _ => state.Update(e.CreateFuturesOptionContractAddedEvent(), e)
        };

    /// <summary>
    /// Creates a <see cref="FuturesOptionContractAddedEvent"/> from an <see cref="AddFuturesOptionContractCommand"/>.
    /// </summary>
    /// <param name="e">The source add command containing entity identifiers, payload, and origin metadata.</param>
    /// <returns>A fully-populated added event ready to be applied to actor state.</returns>
    internal static FuturesOptionContractAddedEvent CreateFuturesOptionContractAddedEvent(this AddFuturesOptionContractCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesOptionContractAddedEvent.Actor, FuturesOptionContractAddedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            Contract = e.Contract,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };

    /// <summary>
    /// Returns the error message for an attempt to add a futures option contract that already exists.
    /// </summary>
    /// <param name="e">The add command that triggered the duplicate-contract error.</param>
    /// <returns>A descriptive error message string.</returns>
    internal static string FuturesOptionContractExistsErrorMsg(this AddFuturesOptionContractCommand e)
        => $"{e.CommandName}: contractId {e.Contract.ContractId} already exists";

}
