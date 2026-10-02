using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands;

/// <summary>Common in-process view of concrete Supervisor operation commands.</summary>
public interface ISupervisorOperationCommand : ICommand<ActorEntityId>
{
    bool PostEvents { get; }
    ActorThreadId Target { get; }
    long ExpectedGeneration { get; }
    string Requester { get; }
    string Reason { get; }
    long TimeoutTicks { get; }
    SupervisorActorOperationKind OperationKind { get; }
}
