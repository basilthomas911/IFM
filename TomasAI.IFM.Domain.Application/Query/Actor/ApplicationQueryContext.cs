using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Application.Query.Actor;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6672", Justification = "Typed actor contexts intentionally retain ILogger of their owning actor, matching the actor context contract and diagnostic category.")]
public sealed class ApplicationQueryContext : QueryActorContext,
IApplicationQueryContext
{
    public ApplicationQueryContext(
        IActorSupervisor supervisor,
        IApplicationStartupStatusStore statusStore,
        ILogger<ApplicationQueryActor> logger)
        : base(supervisor, new ActorMailboxId(ActorType.Query, ApplicationQueryActor.ActorName))
    {
        StatusStore = statusStore ?? throw new ArgumentNullException(nameof(statusStore));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IApplicationStartupStatusStore StatusStore { get; }
    public ILogger<ApplicationQueryActor> Logger { get; }
}
