using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Reference.StrategyCatalog.Command.Actor;

public sealed class StrategyCatalogCommandContext(IActorSupervisor supervisor)
    : CommandActorContext(supervisor, new ActorMailboxId(ActorType.Command, StrategyCatalogCommandActor.ActorName)),
      ICommandActorContext<StrategyCatalogCommandActor>;
