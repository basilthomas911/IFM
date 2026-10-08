using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query;
/// <summary>Handles GetScheduledTaskCatalog without consulting command-state objects.</summary>
public static class GetScheduledTaskCatalog
{
    /// <summary>Reads persisted projections or previews the supplied timing, then sends a typed response.</summary>
    public static async ValueTask ExecuteAsync(this GetScheduledTaskCatalogQuery query, IScheduledTaskReadStore store,
        IQueryActorContext<ScheduledTaskQueryActor> context, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.HostId);
        var result = await store.GetCatalogAsync(query.Environment, query.HostId, cancellationToken).ConfigureAwait(false);
        await context.ReplyAsync<ScheduledTaskCatalog>(query.Subject.ThreadId, query.Subject.Verb,
            result is null ? new ServiceFailed<ScheduledTaskCatalog>(404, "ScheduledTask projection was not found.") : new ServiceOk<ScheduledTaskCatalog>(result)).ConfigureAwait(false);
    }
}
