using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Fund.Transaction.Event.Actor;

/// <summary>
/// Provides the shared event runtime context and Fund transaction services required by
/// <see cref="FundTransactionEventActor"/>.
/// </summary>
public sealed class FundTransactionEventContext :
    EventActorContext,
    IEventActorContext<FundTransactionEventActor>,
    IFundTransactionEventContext
{
    /// <summary>Initializes a Fund transaction event context.</summary>
    /// <param name="supervisor">The actor supervisor that owns the event actor.</param>
    /// <param name="logger">The logger associated with the event actor.</param>
    public FundTransactionEventContext(
        IActorSupervisor supervisor,
        ILogger<FundTransactionEventActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Event, FundTransactionEventActor.Actor))
    {
        Supervisor = IsArgumentNull.Set(supervisor);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc/>
    public IActorSupervisor Supervisor { get; }

    /// <inheritdoc/>
    public ILogger<FundTransactionEventActor> Logger { get; }
}
