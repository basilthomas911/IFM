using TomasAI.IFM.Domain.Supervisor.Health.Query.Actor;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.Queries;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Supervisor.Health.Query;

/// <summary>Reads the latest atomically stored Supervisor health summary.</summary>
public static class GetSupervisorHealthSummary
{
    /// <summary>Replies with the current Supervisor-owned actor metrics snapshot.</summary>
    public static ValueTask ExecuteAsync(this GetSupervisorHealthSummaryQuery query,
        ISupervisorQueryActorContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = context.ActorMetrics.Current;
        var result = new SupervisorHealthSummaryReadModel(
            snapshot.Revision, snapshot.ObservedUtc, snapshot.ExpectedActors,
            snapshot.CollectedActors, snapshot.FailedActors, snapshot.HealthyActors,
            snapshot.DegradedActors, snapshot.CriticalActors, snapshot.UnknownActors,
            snapshot.Quality);
        return context.ReplyAsync(query.Subject.ThreadId, query.Verb,
            new ServiceOk<SupervisorHealthSummaryReadModel>(result));
    }
}
