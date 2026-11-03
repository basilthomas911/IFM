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

    /// <param name="command">
    /// The originating command containing contract business inputs.
    /// The requested contract change.
    /// The originating command supplying routing and audit metadata.
    /// </param>
    extension(AddFuturesOptionContractsCommand command)
    {
        /// <summary>Computes the contract change and applies one source event after failure guards pass.</summary>
        ///
        /// <param name="state">The owning command state; mutation occurs only through Update and Apply.</param>
        /// <returns>The command identity on success, or a business rejection or event-application failure.</returns>
        public ServiceResult<GuidResult> Execute(FuturesOptionContractCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
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
        ///
        /// <param name="futuresOptionContractBatchAddition">The proposed business values and acceptance decision.</param>
        /// <returns>True when computation completes; acceptance is guarded before event application.</returns>
        internal bool Compute(out FuturesOptionContractBatchAddition futuresOptionContractBatchAddition)
        {
            futuresOptionContractBatchAddition = new(command.Contracts);
            return true;
        }

        /// <summary>Creates a source event carrying the computed business values and originating command identity.</summary>
        ///
        /// <param name="futuresOptionContractBatchAddition">The accepted proposed contract change.</param>
        /// <returns>The source event to apply through the owning state.</returns>
        internal FuturesOptionContractsAddedEvent CreateFuturesOptionContractsAddedEvent(FuturesOptionContractBatchAddition futuresOptionContractBatchAddition)
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
}
