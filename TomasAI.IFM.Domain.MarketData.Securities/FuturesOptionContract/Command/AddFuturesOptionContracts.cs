using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketData.Events;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command;

public static class AddFuturesOptionContracts
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="e"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public static bool Execute(this AddFuturesOptionContractsCommand e, FuturesOptionContractCommandState state)
       => state.Update(e.CreateFuturesOptionContractsAddedEvent(), e);

    /// <summary>
    /// Creates a <see cref="FuturesOptionContractsAddedEvent"/> from an <see cref="AddFuturesOptionContractsCommand"/>.
    /// </summary>
    /// <param name="e">The source bulk-add command containing entity identifiers, contracts payload, and origin metadata.</param>
    /// <returns>A fully-populated contracts-added event ready to be applied to actor state.</returns>
    internal static FuturesOptionContractsAddedEvent CreateFuturesOptionContractsAddedEvent(this AddFuturesOptionContractsCommand e)
        => new()
        {
            CommandId = e.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesOptionContractsAddedEvent.Actor, FuturesOptionContractsAddedEvent.Verb, e.EntityId.Format()),
            EntityId = e.EntityId,
            Contracts = e.Contracts,
            CreatedOn = e.OriginatedOn,
            CreatedBy = e.OriginatedBy
        };
}
