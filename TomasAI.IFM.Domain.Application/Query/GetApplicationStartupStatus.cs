using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.Application.Shared.Queries;
using TomasAI.IFM.Domain.Application.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Application.Query;

/// <summary>Handles <see cref="GetApplicationStartupStatusQuery"/>.</summary>
public static class GetApplicationStartupStatus
{
    /// <summary>Returns the current immutable application startup status.</summary>
    public static ValueTask ExecuteAsync(this GetApplicationStartupStatusQuery query, IApplicationQueryContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<ApplicationStartupStatus>(context.StatusStore.Current));
    }
}
