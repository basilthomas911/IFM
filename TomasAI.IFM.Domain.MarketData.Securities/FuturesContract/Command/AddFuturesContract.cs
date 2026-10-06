using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command;

/// <summary>Handles AddFuturesContract through pure computation and state-owned event application.</summary>
public static class AddFuturesContract
{
    /// <summary>Computes the contract change and applies one source event after failure guards pass.</summary>
    /// <param name="command">The originating command containing contract business inputs.</param>
    /// <param name="state">The owning command state; mutation occurs only through Update and Apply.</param>
    /// <returns>The command identity on success, or a business rejection or event-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this AddFuturesContractCommand command, FuturesContractCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply FuturesContractAddedEvent";
        var updated = command.Compute(state.FuturesContractExists(command.Contract.Id, command.Overwrite), out var futuresContractAddition) switch
        {
            _ when !futuresContractAddition.Accepted
                => command.UpdateFailed(ref errorMsg, $"{command.CommandName}: contract {command.Contract.ContractId} already exists"),
            _ => state.Update(command.CreateFuturesContractAddedEvent(futuresContractAddition), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes immutable proposed contract values without changing state or pending events.</summary>
    /// <param name="command">The requested contract change.</param>
    /// <param name="contractChangeRejected">Whether the existing contract state rejects this operation under its overwrite policy.</param>
    /// <param name="futuresContractAddition">The proposed business values and acceptance decision.</param>
    /// <returns>True when computation completes; acceptance is guarded before event application.</returns>
    internal static bool Compute(this AddFuturesContractCommand command, bool contractChangeRejected, out FuturesContractAddition futuresContractAddition)
    {
        futuresContractAddition = new(command.Contract, !contractChangeRejected);
        return true;
    }

    /// <summary>Creates a source event carrying the computed business values and originating command identity.</summary>
    /// <param name="command">The originating command supplying routing and audit metadata.</param>
    /// <param name="futuresContractAddition">The accepted proposed contract change.</param>
    /// <returns>The source event to apply through the owning state.</returns>
    internal static FuturesContractAddedEvent CreateFuturesContractAddedEvent(this AddFuturesContractCommand command, FuturesContractAddition futuresContractAddition)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesContractAddedEvent.Actor, FuturesContractAddedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            Contract = futuresContractAddition.FuturesContract,
            CreatedOn = command.OriginatedOn,
            CreatedBy = command.OriginatedBy
        };
}
