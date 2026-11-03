using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command;

/// <summary>Handles RemoveFuturesOptionContract through pure computation and state-owned event application.</summary>
public static class RemoveFuturesOptionContract
{

    /// <param name="command">
    /// The originating command containing contract business inputs.
    /// The requested contract change.
    /// The originating command supplying routing and audit metadata.
    /// </param>
    extension(RemoveFuturesOptionContractCommand command)
    {
        /// <summary>Computes the contract change and applies one source event after failure guards pass.</summary>
        ///
        /// <param name="state">The owning command state; mutation occurs only through Update and Apply.</param>
        /// <returns>The command identity on success, or a business rejection or event-application failure.</returns>
        public ServiceResult<GuidResult> Execute(FuturesOptionContractCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            var errorMsg = $"{command.CommandName}: unable to apply FuturesOptionContractRemovedEvent";
            var updated = command.Compute(state.FuturesOptionContractDoesNotExist(command.ContractId, command.Overwrite), out var futuresOptionContractRemoval) switch
            {
                _ when !futuresOptionContractRemoval.Accepted
                    => command.UpdateFailed(ref errorMsg, $"{command.CommandName}: contract {command.ContractId} does not exist"),
                _ => state.Update(command.CreateFuturesOptionContractRemovedEvent(futuresOptionContractRemoval), command)
            };
            return updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed(errorMsg);
        }

        /// <summary>Computes immutable proposed contract values without changing state or pending events.</summary>
        ///
        /// <param name="contractChangeRejected">Whether the existing contract state rejects this operation under its overwrite policy.</param>
        /// <param name="futuresOptionContractRemoval">The proposed business values and acceptance decision.</param>
        /// <returns>True when computation completes; acceptance is guarded before event application.</returns>
        internal bool Compute(bool contractChangeRejected, out FuturesOptionContractRemoval futuresOptionContractRemoval)
        {
            futuresOptionContractRemoval = new(command.ContractId, !contractChangeRejected);
            return true;
        }

        /// <summary>Creates a source event carrying the computed business values and originating command identity.</summary>
        ///
        /// <param name="futuresOptionContractRemoval">The accepted proposed contract change.</param>
        /// <returns>The source event to apply through the owning state.</returns>
        internal FuturesOptionContractRemovedEvent CreateFuturesOptionContractRemovedEvent(FuturesOptionContractRemoval futuresOptionContractRemoval)
            => new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(ActorType.Event, FuturesOptionContractRemovedEvent.Actor, FuturesOptionContractRemovedEvent.Verb, command.EntityId.Format()),
                EntityId = command.EntityId,
                ContractId = futuresOptionContractRemoval.FuturesOptionContractId,
                DeletedOn = command.OriginatedOn,
                DeletedBy = command.OriginatedBy
            };
    }
}
