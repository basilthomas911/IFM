using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Supervisor.Shared.Enums;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;

/// <summary>Runs one generation-fenced thread restart at a time without blocking the polling thread.</summary>
public sealed class SupervisorHealthActionCoordinator(
    IActorSupervisor supervisor,
    IActorRuntimeMetricsSourceProvider sources,
    SupervisorOperationStore operations,
    SupervisorIncidentStore incidents,
    SupervisorHistoryStore history,
    SupervisorHealthActionOptions options,
    ILogger<SupervisorHealthActionCoordinator> logger) : ISupervisorHealthActionCoordinator
{
    readonly ConcurrentDictionary<ActorThreadId, byte> _active = [];

    public void Observe(SupervisorActorMetricsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        history.Observe(snapshot);
        incidents.Observe(snapshot);
        if (!options.AutomaticMutationEnabled) return;
        foreach (var actor in snapshot.Actors)
            foreach (var thread in actor.Threads)
                // This quote router has a pipeline-owned three-attempt budget and hard-reset escalation.
                if (actor.ActorId != new ActorMailboxId(ActorType.Realtime, "FuturesOptionRealtime")
                    && thread.RestartRequired && _active.TryAdd(thread.ThreadId, 0))
                    _ = Task.Run(() => RestartAsync(thread.ThreadId, thread.Generation));
    }

    async Task RestartAsync(ActorThreadId threadId, long expectedGeneration)
    {
        var operation = operations.Started(threadId, expectedGeneration);
        var outcome = SupervisorOperationOutcome.Failed;
        var reason = "Automatic restart failed before completion.";
        try
        {
            var current = sources.CaptureActorMetricsSources()
                .FirstOrDefault(source => source.ActorId == threadId.MailboxId)?.CaptureSnapshot();
            var mailbox = current?.Mailboxes.FirstOrDefault(value => value.ThreadId == threadId);
            if (mailbox is null || !mailbox.IsAdmissionOpen || mailbox.Generation != expectedGeneration)
            {
                outcome = SupervisorOperationOutcome.Rejected;
                reason = "The health observation was stale or mailbox admission is closed; explicit resume is required.";
                return;
            }
            global::TomasAI.IFM.Domain.Supervisor.Shared.PersistenceDiagnosticLog.Write6(logger, null, nameof(RestartAsync), threadId, expectedGeneration, "RestartRequired", TimeSpan.FromMinutes(1));
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var restarted = await supervisor.RestartAsync(threadId, expectedGeneration, TimeSpan.FromMinutes(1), timeout.Token).ConfigureAwait(false);
            outcome = restarted ? SupervisorOperationOutcome.Succeeded : SupervisorOperationOutcome.Rejected;
            reason = restarted ? "Actor thread restarted successfully." : "Actor thread generation changed or the mailbox could not drain safely.";
            if (restarted) incidents.RecordRestart(threadId, expectedGeneration + 1);
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException and not AccessViolationException)
        {
            outcome = exception is OperationCanceledException ? SupervisorOperationOutcome.TimedOut : SupervisorOperationOutcome.Failed;
            reason = exception.Message;
            SupervisorHealthActionLog.RestartFailed(logger, threadId, expectedGeneration, exception);
        }
        finally
        {
            global::TomasAI.IFM.Domain.Supervisor.Shared.PersistenceDiagnosticLog.Write7(logger, null, nameof(RestartAsync), threadId, expectedGeneration, outcome, reason);
            operations.Completed(operation, outcome, reason);
            _active.TryRemove(threadId, out _);
        }
    }
}
