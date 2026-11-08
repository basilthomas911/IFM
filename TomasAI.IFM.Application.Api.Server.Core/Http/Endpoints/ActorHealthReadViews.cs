using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;

/// <summary>Small diagnostic views alongside the unchanged compatibility endpoint.</summary>
public static class ActorHealthReadViews
{
    public sealed record Summary(DateTime ObservedUtc, SupervisorActorHealthStatus OverallStatus, int ActorCount,
        int RunningActorCount, int ProcessingMailboxCount, int QueuedMessageCount, int FailureCount,
        int ProjectorCount, int WorkerCount);
    public sealed record Page<T>(DateTime ObservedUtc, int Offset, int Total, IReadOnlyList<T> Rows);

    public static Summary Summarize(SupervisorRuntimeSnapshot snapshot) => new(snapshot.ObservedUtc, snapshot.OverallStatus,
        snapshot.ActorCount, snapshot.RunningActorCount, snapshot.ProcessingMailboxCount, snapshot.QueuedMessageCount,
        snapshot.Failures.Count, snapshot.Projectors.Count, snapshot.Workers?.Count ?? 0);

    public static Page<SupervisorActorSnapshot> Actors(SupervisorRuntimeSnapshot snapshot, int offset, int limit)
    {
        ValidatePage(offset, limit);
        return new(snapshot.ObservedUtc, offset, snapshot.Actors.Count,
            snapshot.Actors.Skip(offset).Take(limit).Select(actor => actor with { Mailboxes = [] }).ToArray());
    }

    public static void ValidatePage(int offset, int limit)
    {
        if (offset is < 0 or > 10080 || limit is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(limit), "Offset must be 0..10080 and limit 1..256.");
    }
}
