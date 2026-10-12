using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Actor.Client;

/// <summary>Provides the typed runtime context used by <see cref="TestEventActor"/>.</summary>
public sealed class TestEventContext : EventActorContext, ITestEventContext
{
    /// <summary>Initializes a new typed test event context.</summary>
    /// <param name="supervisor">The actor supervisor.</param>
    /// <param name="logger">The actor logger.</param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Context injects the owning actor logger category, matching actor diagnostics.")]
    public TestEventContext(
        IActorSupervisor supervisor,
        ILogger<TestEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, TestEventActor.MailboxName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }

    /// <inheritdoc/>
    public ILogger<TestEventActor> Logger { get; }
}
