using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Event.Actor;

/// <summary>
/// Provides the shared event actor runtime context and Fund-specific services required by <see cref="FundEventActor"/>.
/// </summary>
public sealed class FundEventContext :
    EventActorContext,
    IEventActorContext<FundEventActor>,
    IFundEventContext
{
    /// <summary>
    /// Initializes a Fund event context.
    /// </summary>
    /// <param name="supervisor">The actor supervisor that owns the Fund event actor.</param>
    /// <param name="logger">The logger associated with <see cref="FundEventActor"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="supervisor"/> or <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    public FundEventContext(
        IActorSupervisor supervisor,
        ILogger<FundEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FundEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }

    /// <inheritdoc/>
    public ILogger<FundEventActor> Logger { get; }
}
