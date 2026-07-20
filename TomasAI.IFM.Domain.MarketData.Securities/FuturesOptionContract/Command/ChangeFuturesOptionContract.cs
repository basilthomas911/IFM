using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Exceptions;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command;

public static class ChangeFuturesOptionContract
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    /// <exception cref="ChangeFuturesOptionContractException"></exception>
    public static bool Execute(this ChangeFuturesOptionContractCommand e, FuturesOptionContractCommandState state)
        => e switch
        {
            _ when state.FuturesOptionContractDoesNotExist(e.ContractId, e.Overwrite) => throw new ChangeFuturesOptionContractException(e.FuturesOptionContractDoesNotExistErrorMsg()),
            _ => state.Update(e.CreateFuturesOptionContractChangedEvent(), e)
        };

    /// <summary>
    /// Creates a <see cref="FuturesOptionContractChangedEvent"/> from a <see cref="ChangeFuturesOptionContractCommand"/>.
    /// </summary>
    /// <param name="e">The source change command containing entity identifiers, payload, and origin metadata.</param>
    /// <returns>A fully-populated changed event ready to be applied to actor state.</returns>
    internal static FuturesOptionContractChangedEvent CreateFuturesOptionContractChangedEvent(this ChangeFuturesOptionContractCommand e)
        => new()
        {
            Subject = new ActorSubject(ActorType.Event, FuturesOptionContractChangedEvent.Actor, FuturesOptionContractChangedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            OriginalContractId = e.ContractId,
            Contract = e.Contract,
            UpdatedOn = e.OriginatedOn,
            UpdatedBy = e.OriginatedBy
        };

    /// <summary>
    /// Returns the error message for an attempt to change a futures option contract that does not exist.
    /// </summary>
    /// <param name="e">The change command that triggered the missing-contract error.</param>
    /// <returns>A descriptive error message string.</returns>
    static string FuturesOptionContractDoesNotExistErrorMsg(this ChangeFuturesOptionContractCommand e)
        => $"Futures option contract with id {e.ContractId} does not exist";


}
