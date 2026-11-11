using TomasAI.IFM.Domain.MarketData.Securities.Persistence;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command;

/// <summary>Accepts only a prepared rollover against the owning catalog's committed revision.</summary>
public static class ReplaceFuturesRolloverSet
{
    extension(ReplaceFuturesRolloverSetCommand command)
    {
        /// <summary>Applies one complete rollover event after checking the preparation fence.</summary>
        /// <param name="state">The owning Futures catalog command state.</param>
        /// <returns>The accepted command ID or a stale/invalid preparation failure.</returns>
        public ServiceResult<GuidResult> Execute(FuturesContractCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(state);
            if (state.CommittedStreamVersion != command.ExpectedCatalogRevision)
                return new ServiceFailed<GuidResult>(ReplaceFuturesRolloverSetCommand.CatalogChanged, "The futures catalog changed during rollover preparation.");
            if (!FuturesRolloverPreparation.IsValid(command.Rollover, command.ChangedContracts, command.EntityId))
                return new ServiceFailed<GuidResult>(command.ErrorCode, "The prepared futures rollover set is invalid.");
            var fact = new FuturesContractRolloverSetReplacedEvent
            {
                CommandId = command.CommandId,
                AggregateId = SecuritiesCatalogStreams.Futures,
                Subject = new(ActorType.Event, FuturesContractAddedEvent.Actor, "RolloverSetReplaced", command.Rollover.Symbol),
                Rollover = command.Rollover,
                ChangedContracts = command.ChangedContracts
            };
            return state.Update(fact, command)
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : new ServiceFailed<GuidResult>(command.ErrorCode, "The owning catalog could not apply the rollover fact.");
        }
    }
}
