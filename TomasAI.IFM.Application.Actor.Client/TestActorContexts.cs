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

/// <summary>Provides the typed runtime context used by <see cref="TestCommandActor"/>.</summary>
public sealed class TestCommandContext : CommandActorContext, ITestCommandContext
{
    /// <summary>Initializes a new typed test command context.</summary>
    /// <param name="supervisor">The actor supervisor.</param>
    /// <param name="logger">The actor logger.</param>
    public TestCommandContext(
        IActorSupervisor supervisor,
        ILogger<TestCommandActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Command, TestCommandActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }

    /// <inheritdoc/>
    public ILogger<TestCommandActor> Logger { get; }
}

/// <summary>Defines the readonly runtime services required by <see cref="TestEventActor"/>.</summary>
public interface ITestEventContext : IEventActorContext<TestEventActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }

    /// <summary>Gets the actor logger.</summary>
    ILogger<TestEventActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="TestEventActor"/>.</summary>
public sealed class TestEventContext : EventActorContext, ITestEventContext
{
    /// <summary>Initializes a new typed test event context.</summary>
    /// <param name="supervisor">The actor supervisor.</param>
    /// <param name="logger">The actor logger.</param>
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

/// <summary>Defines the readonly runtime services required by <see cref="TestQueryActor"/>.</summary>
public interface ITestQueryContext : IQueryActorContext<TestQueryActor>
{
    /// <summary>Gets the actor supervisor.</summary>
    IActorSupervisor Supervisor { get; }

    /// <summary>Gets the actor logger.</summary>
    ILogger<TestQueryActor> Logger { get; }
}

/// <summary>Provides the typed runtime context used by <see cref="TestQueryActor"/>.</summary>
public sealed class TestQueryContext : QueryActorContext, ITestQueryContext
{
    /// <summary>Initializes a new typed test query context.</summary>
    /// <param name="supervisor">The actor supervisor.</param>
    /// <param name="logger">The actor logger.</param>
    public TestQueryContext(
        IActorSupervisor supervisor,
        ILogger<TestQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, TestQueryActor.ActorName))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }

    /// <inheritdoc/>
    public ILogger<TestQueryActor> Logger { get; }
}
