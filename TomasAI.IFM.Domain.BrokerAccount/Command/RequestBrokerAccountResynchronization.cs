using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command;

/// <summary>Records the operator request before collecting evidence from the broker.</summary>
public static class RequestBrokerAccountResynchronization
{
    extension(RequestBrokerAccountResynchronizationCommand command)
    {
        public ServiceResult<GuidResult> Execute(BrokerAccountCommandState state)
        {
            var request = new BrokerAccountResynchronizationRequestedEvent
            {
                CommandId = command.CommandId,
                EntityId = command.EntityId,
                Subject = new(ActorType.Event, BrokerAccountActorNames.Event, BrokerAccountResynchronizationRequestedEvent.Verb,
                    command.EntityId.Format()),
                Operation = BrokerAccountOperation.RequestBrokerAccountResynchronization,
                RequestedAtUtc = command.RequestedAtUtc
            };
            return state.Update(request, command)
                ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
                : command.UpdateFailed("BrokerAccount.STATE.APPLY_FAILED;unable to record resynchronization request");
        }
    }
}
