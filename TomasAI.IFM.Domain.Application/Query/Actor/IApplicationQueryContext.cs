using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Application.Query.Actor;

public interface IApplicationQueryContext : IQueryActorContext<ApplicationQueryActor>
{
    IApplicationStartupStatusStore StatusStore { get; }
    ILogger<ApplicationQueryActor> Logger { get; }
}
