using TomasAI.IFM.Application.EventProjector.Contracts;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.BrokerAccount.Command.Actor;

/// <summary>Services used by BrokerAccount command handlers.</summary>
public interface IBrokerAccountCommandContext : ICommandActorContext<BrokerAccountCommandActor>
{
    IEventProjector<BrokerAccountCommandActor> EventProjector { get; }
    BrokerAccountObservationBridge ObservationBridge { get; }
    IEventSourceActorStateRepository<BrokerAccountCommandState> StateRepository { get; }
    ILogger<BrokerAccountCommandActor> Logger { get; }
}
