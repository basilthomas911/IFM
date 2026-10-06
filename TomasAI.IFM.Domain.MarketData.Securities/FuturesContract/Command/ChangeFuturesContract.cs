using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command;

/// <summary>Handles ChangeFuturesContract through pure computation and state-owned event application.</summary>
public static class ChangeFuturesContract
{
    /// <summary>Computes the contract change and applies one source event after failure guards pass.</summary>
    /// <param name="command">The originating command containing contract business inputs.</param>
    /// <param name="state">The owning command state; mutation occurs only through Update and Apply.</param>
    /// <returns>The command identity on success, or a business rejection or event-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this ChangeFuturesContractCommand command, FuturesContractCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply FuturesContractChangedEvent";
        var updated = command.Compute(state.FuturesContractDoesNotExist(command.ContractId, command.Overwrite), out var futuresContractAmendment) switch
        {
            _ when !futuresContractAmendment.Accepted
                => command.UpdateFailed(ref errorMsg, $"{command.CommandName}: contract {command.ContractId} does not exist"),
            _ => state.Update(command.CreateFuturesContractChangedEvent(futuresContractAmendment), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes immutable proposed contract values without changing state or pending events.</summary>
    /// <param name="command">The requested contract change.</param>
    /// <param name="contractChangeRejected">Whether the existing contract state rejects this operation under its overwrite policy.</param>
    /// <param name="futuresContractAmendment">The proposed business values and acceptance decision.</param>
    /// <returns>True when computation completes; acceptance is guarded before event application.</returns>
    internal static bool Compute(this ChangeFuturesContractCommand command, bool contractChangeRejected, out FuturesContractAmendment futuresContractAmendment)
    {
        futuresContractAmendment = new(command.ContractId, command.Contract, !contractChangeRejected);
        return true;
    }

    /// <summary>Creates a source event carrying the computed business values and originating command identity.</summary>
    /// <param name="command">The originating command supplying routing and audit metadata.</param>
    /// <param name="futuresContractAmendment">The accepted proposed contract change.</param>
    /// <returns>The source event to apply through the owning state.</returns>
    internal static FuturesContractChangedEvent CreateFuturesContractChangedEvent(this ChangeFuturesContractCommand command, FuturesContractAmendment futuresContractAmendment)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesContractChangedEvent.Actor, FuturesContractChangedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            OriginalContractId = futuresContractAmendment.OriginalFuturesContractId,
            Contract = futuresContractAmendment.FuturesContract,
            UpdatedOn = command.OriginatedOn,
            UpdatedBy = command.OriginatedBy
        };
}
