using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command;

internal static class BrokerAccountCommandResult
{
    internal static ServiceResult<GuidResult> Apply(
        BrokerAccountCommand command,
        BrokerAccountCommandState state,
        BrokerAccountDefinition next)
    {
        var applied = state.Update(new BrokerAccountChangedEvent
        {
            Subject = new(ActorType.Event, BrokerAccountChangedEvent.Actor,
                BrokerAccountChangedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            State = next
        }, command);
        return applied
            ? new ServiceOk<GuidResult>(new(command.CommandId))
            : command.UpdateFailed("BA.STATE.APPLY_FAILED");
    }

    internal static BrokerAccountOperationalGate Gate(BrokerAccountDefinition state) =>
        state.ManualHold || state.QualificationStatus != BrokerAccountQualificationStatus.Accepted ||
        state.Snapshot is not { Complete: true, NewRiskAllowed: true }
            ? BrokerAccountOperationalGate.Closed
            : BrokerAccountOperationalGate.Open;
}
