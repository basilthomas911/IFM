using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query;
/// <summary>Handles GetScheduledTaskHostHealth without consulting command-state objects.</summary>
public static class GetScheduledTaskHostHealth
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3236", Justification = "Validation targets a member of the request, but ParamName intentionally identifies the containing method argument so callers can identify the rejected request.")]     /// <summary>Reads persisted projections or previews the supplied timing, then sends a typed response.</summary>
    public static async ValueTask ExecuteAsync(this GetScheduledTaskHostHealthQuery query, IScheduledTaskReadStore store,
        IQueryActorContext<ScheduledTaskQueryActor> context, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Environment, nameof(query));
        ArgumentException.ThrowIfNullOrWhiteSpace(query.HostId, nameof(query));
        var result = (await store.GetCatalogAsync(query.Environment, query.HostId, cancellationToken).ConfigureAwait(false))?.HostCapability;
        await context.ReplyAsync<ScheduledTaskHostCapability>(query.Subject.ThreadId, query.Subject.Verb,
            result is null ? new ServiceFailed<ScheduledTaskHostCapability>(404, "ScheduledTask projection was not found.") : new ServiceOk<ScheduledTaskHostCapability>(result)).ConfigureAwait(false);
    }
}
