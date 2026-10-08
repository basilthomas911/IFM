using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Query;
/// <summary>Handles GetScheduledTasksDashboard without consulting command-state objects.</summary>
public static class GetScheduledTasksDashboard
{
    /// <summary>Reads persisted projections or previews the supplied timing, then sends a typed response.</summary>
    public static async ValueTask ExecuteAsync(this GetScheduledTasksDashboardQuery query, IScheduledTaskReadStore store,
        IQueryActorContext<ScheduledTaskQueryActor> context, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.HostId);
        var catalog = await store.GetCatalogAsync(query.Environment, query.HostId, cancellationToken).ConfigureAwait(false);
        var schedules = await store.GetDefinitionsAsync(query.Environment, query.HostId, query.PageSize, cancellationToken).ConfigureAwait(false);
        var runs = query.SelectedScheduleId.IsValid ? await store.GetRunsAsync(query.Environment, query.HostId, query.SelectedScheduleId, 100, cancellationToken).ConfigureAwait(false) : [];
        var result = new ScheduledTasksDashboard { Catalog = catalog, Schedules = schedules, RecentRuns = runs, ReadAtUtc = clock.GetUtcNow() };
        await context.ReplyAsync<ScheduledTasksDashboard>(query.Subject.ThreadId, query.Subject.Verb,
            result is null ? new ServiceFailed<ScheduledTasksDashboard>(404, "ScheduledTask projection was not found.") : new ServiceOk<ScheduledTasksDashboard>(result)).ConfigureAwait(false);
    }
}
