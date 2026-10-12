using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Actor.Client;

/// <summary>Defines the readonly runtime services required by <see cref="TestCommandActor"/>.</summary>
public interface ITestCommandContext : ICommandActorContext<TestCommandActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }

    /// <summary>Gets the actor logger.</summary>
    ILogger<TestCommandActor> Logger { get; }
}
