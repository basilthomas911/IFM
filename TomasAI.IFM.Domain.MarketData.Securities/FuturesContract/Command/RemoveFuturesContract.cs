using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command;

/// <summary>Handles RemoveFuturesContract through pure computation and state-owned event application.</summary>
public static class RemoveFuturesContract
{

    /// <param name="command">
    /// The originating command containing contract business inputs.
    /// The requested contract change.
    /// The originating command supplying routing and audit metadata.
    /// </param>
    extension(RemoveFuturesContractCommand command)
    {
        /// <summary>Computes the contract change and applies one source event after failure guards pass.</summary>
        ///
        /// <param name="state">The owning command state; mutation occurs only through Update and Apply.</param>
        /// <returns>The command identity on success, or a business rejection or event-application failure.</returns>
        public ServiceResult<GuidResult> Execute(FuturesContractCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            var errorMsg = $"{command.CommandName}: unable to apply FuturesContractRemovedEvent";
            var updated = command.Compute(state.FuturesContractDoesNotExist(command.ContractId, command.Overwrite), out var futuresContractRemoval) switch
            {
                _ when !futuresContractRemoval.Accepted
                    => command.UpdateFailed(ref errorMsg, $"{command.CommandName}: contract {command.ContractId} does not exist"),
                _ => state.Update(command.CreateFuturesContractRemovedEvent(futuresContractRemoval), command)
            };
            return updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed(errorMsg);
        }

        /// <summary>Computes immutable proposed contract values without changing state or pending events.</summary>
        ///
        /// <param name="contractChangeRejected">Whether the existing contract state rejects this operation under its overwrite policy.</param>
        /// <param name="futuresContractRemoval">The proposed business values and acceptance decision.</param>
        /// <returns>True when computation completes; acceptance is guarded before event application.</returns>
        internal bool Compute(bool contractChangeRejected, out FuturesContractRemoval futuresContractRemoval)
        {
            futuresContractRemoval = new(command.ContractId, !contractChangeRejected);
            return true;
        }

        /// <summary>Creates a source event carrying the computed business values and originating command identity.</summary>
        ///
        /// <param name="futuresContractRemoval">The accepted proposed contract change.</param>
        /// <returns>The source event to apply through the owning state.</returns>
        internal FuturesContractRemovedEvent CreateFuturesContractRemovedEvent(FuturesContractRemoval futuresContractRemoval)
            => new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(ActorType.Event, FuturesContractRemovedEvent.Actor, FuturesContractRemovedEvent.Verb, command.EntityId.Format()),
                EntityId = command.EntityId,
                ContractId = futuresContractRemoval.FuturesContractId,
                DeletedOn = command.OriginatedOn,
                DeletedBy = command.OriginatedBy
            };
    }
}
