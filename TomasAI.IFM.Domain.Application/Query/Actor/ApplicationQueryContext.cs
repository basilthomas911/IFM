using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Application.Actor.Query.Actor;

public interface IApplicationQueryContext : IQueryActorContext<ApplicationQueryActor>
{
    IApplicationStartupStatusStore StatusStore { get; }
    ILogger<ApplicationQueryActor> Logger { get; }
}

public sealed class ApplicationQueryContext : QueryActorContext,
    IQueryActorContext<ApplicationQueryActor>, IApplicationQueryContext
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
