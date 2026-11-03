using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command;

/// <summary>Handles AddFuturesOptionContract through pure computation and state-owned event application.</summary>
public static class AddFuturesOptionContract
{

    /// <param name="command">
    /// The originating command containing contract business inputs.
    /// The requested contract change.
    /// The originating command supplying routing and audit metadata.
    /// </param>
    extension(AddFuturesOptionContractCommand command)
    {
        /// <summary>Computes the contract change and applies one source event after failure guards pass.</summary>
        ///
        /// <param name="state">The owning command state; mutation occurs only through Update and Apply.</param>
        /// <returns>The command identity on success, or a business rejection or event-application failure.</returns>
        public ServiceResult<GuidResult> Execute(FuturesOptionContractCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            var errorMsg = $"{command.CommandName}: unable to apply FuturesOptionContractAddedEvent";
            var updated = command.Compute(state.FuturesOptionContractExists(command.Contract.ContractId, command.Overwrite), out var futuresOptionContractAddition) switch
            {
                _ when !futuresOptionContractAddition.Accepted
                    => command.UpdateFailed(ref errorMsg, $"{command.CommandName}: contract {command.Contract.ContractId} already exists"),
                _ => state.Update(command.CreateFuturesOptionContractAddedEvent(futuresOptionContractAddition), command)
            };
            return updated
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed(errorMsg);
        }

        /// <summary>Computes immutable proposed contract values without changing state or pending events.</summary>
        ///
        /// <param name="contractChangeRejected">Whether the existing contract state rejects this operation under its overwrite policy.</param>
        /// <param name="futuresOptionContractAddition">The proposed business values and acceptance decision.</param>
        /// <returns>True when computation completes; acceptance is guarded before event application.</returns>
        internal bool Compute(bool contractChangeRejected, out FuturesOptionContractAddition futuresOptionContractAddition)
        {
            futuresOptionContractAddition = new(command.Contract, !contractChangeRejected);
            return true;
        }

        /// <summary>Creates a source event carrying the computed business values and originating command identity.</summary>
        ///
        /// <param name="futuresOptionContractAddition">The accepted proposed contract change.</param>
        /// <returns>The source event to apply through the owning state.</returns>
        internal FuturesOptionContractAddedEvent CreateFuturesOptionContractAddedEvent(FuturesOptionContractAddition futuresOptionContractAddition)
            => new()
            {
                CommandId = command.CommandId,
                Subject = new ActorSubject(ActorType.Event, FuturesOptionContractAddedEvent.Actor, FuturesOptionContractAddedEvent.Verb, command.EntityId.Format()),
                EntityId = command.EntityId,
                Contract = futuresOptionContractAddition.FuturesOptionContract,
                CreatedOn = command.OriginatedOn,
                CreatedBy = command.OriginatedBy
            };
    }
}
