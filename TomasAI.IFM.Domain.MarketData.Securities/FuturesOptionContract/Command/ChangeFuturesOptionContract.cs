using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command;

/// <summary>Handles ChangeFuturesOptionContract through pure computation and state-owned event application.</summary>
public static class ChangeFuturesOptionContract
{
    /// <summary>Computes the contract change and applies one source event after failure guards pass.</summary>
    /// <param name="command">The originating command containing contract business inputs.</param>
    /// <param name="state">The owning command state; mutation occurs only through Update and Apply.</param>
    /// <returns>The command identity on success, or a business rejection or event-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this ChangeFuturesOptionContractCommand command, FuturesOptionContractCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply FuturesOptionContractChangedEvent";
        var updated = command.Compute(state.FuturesOptionContractDoesNotExist(command.ContractId, command.Overwrite), out var futuresOptionContractAmendment) switch
        {
            _ when !futuresOptionContractAmendment.Accepted
                => command.UpdateFailed(ref errorMsg, $"{command.CommandName}: contract {command.ContractId} does not exist"),
            _ => state.Update(command.CreateFuturesOptionContractChangedEvent(futuresOptionContractAmendment), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes immutable proposed contract values without changing state or pending events.</summary>
    /// <param name="command">The requested contract change.</param>
    /// <param name="contractChangeRejected">Whether the existing contract state rejects this operation under its overwrite policy.</param>
    /// <param name="futuresOptionContractAmendment">The proposed business values and acceptance decision.</param>
    /// <returns>True when computation completes; acceptance is guarded before event application.</returns>
    internal static bool Compute(this ChangeFuturesOptionContractCommand command, bool contractChangeRejected, out FuturesOptionContractAmendment futuresOptionContractAmendment)
    {
        futuresOptionContractAmendment = new(command.ContractId, command.Contract, !contractChangeRejected);
        return true;
    }

    /// <summary>Creates a source event carrying the computed business values and originating command identity.</summary>
    /// <param name="command">The originating command supplying routing and audit metadata.</param>
    /// <param name="futuresOptionContractAmendment">The accepted proposed contract change.</param>
    /// <returns>The source event to apply through the owning state.</returns>
    internal static FuturesOptionContractChangedEvent CreateFuturesOptionContractChangedEvent(this ChangeFuturesOptionContractCommand command, FuturesOptionContractAmendment futuresOptionContractAmendment)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesOptionContractChangedEvent.Actor, FuturesOptionContractChangedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            OriginalContractId = futuresOptionContractAmendment.OriginalFuturesOptionContractId,
            Contract = futuresOptionContractAmendment.FuturesOptionContract,
            UpdatedOn = command.OriginatedOn,
            UpdatedBy = command.OriginatedBy
        };
}
