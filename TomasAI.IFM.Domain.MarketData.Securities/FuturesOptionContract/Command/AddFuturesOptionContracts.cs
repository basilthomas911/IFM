using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command;

/// <summary>Handles AddFuturesOptionContracts through pure computation and state-owned event application.</summary>
public static class AddFuturesOptionContracts
{
    /// <summary>Computes the contract change and applies one source event after failure guards pass.</summary>
    /// <param name="command">The originating command containing contract business inputs.</param>
    /// <param name="state">The owning command state; mutation occurs only through Update and Apply.</param>
    /// <returns>The command identity on success, or a business rejection or event-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this AddFuturesOptionContractsCommand command, FuturesOptionContractCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply FuturesOptionContractsAddedEvent";
        var updated = command.Compute(out var futuresOptionContractBatchAddition) switch
        {
            _ => state.Update(command.CreateFuturesOptionContractsAddedEvent(futuresOptionContractBatchAddition), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes immutable proposed contract values without changing state or pending events.</summary>
    /// <param name="command">The requested contract change.</param>
    /// <param name="futuresOptionContractBatchAddition">The proposed business values and acceptance decision.</param>
    /// <returns>True when computation completes; acceptance is guarded before event application.</returns>
    internal static bool Compute(this AddFuturesOptionContractsCommand command, out FuturesOptionContractBatchAddition futuresOptionContractBatchAddition)
    {
        futuresOptionContractBatchAddition = new(command.Contracts);
        return true;
    }

    /// <summary>Creates a source event carrying the computed business values and originating command identity.</summary>
    /// <param name="command">The originating command supplying routing and audit metadata.</param>
    /// <param name="futuresOptionContractBatchAddition">The accepted proposed contract change.</param>
    /// <returns>The source event to apply through the owning state.</returns>
    internal static FuturesOptionContractsAddedEvent CreateFuturesOptionContractsAddedEvent(this AddFuturesOptionContractsCommand command, FuturesOptionContractBatchAddition futuresOptionContractBatchAddition)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesOptionContractsAddedEvent.Actor, FuturesOptionContractsAddedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            Contracts = futuresOptionContractBatchAddition.FuturesOptionContracts,
            CreatedOn = command.OriginatedOn,
            CreatedBy = command.OriginatedBy
        };
}
