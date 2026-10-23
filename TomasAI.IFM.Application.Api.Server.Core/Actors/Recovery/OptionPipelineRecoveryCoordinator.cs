using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Api.Server.Core.HostedServices;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;

public sealed record OptionPipelineInspection(DateOnly? ValueDate, string? Failure, string[] Positions);
public interface IOptionPipelineRecoveryRuntime
{
    OptionPipelineInspection Inspect();
    Task RecoverAndVerifyAsync(Guid operationId, OptionPipelineInspection inspection, DateTimeOffset deadlineUtc, CancellationToken cancellationToken);
}

/// <summary>One independent deadline covers emergency reset and fresh end-to-end proof. External escalation owns API replacement.</summary>
public sealed class OptionPipelineRecoveryCoordinator(IOptionPipelineRecoveryRuntime runtime,
    ILogger<OptionPipelineRecoveryCoordinator> logger, TimeProvider? timeProvider = null, TimeSpan? recoveryBudget = null)
{
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    readonly TimeSpan budget = Validate(recoveryBudget ?? TimeSpan.FromMinutes(2));
    readonly object gate = new();
    Episode? current;
    public string State { get { lock (gate) return current?.State ?? "Healthy"; } }
    public Task? Completion { get { lock (gate) return current?.Work; } }
    public MarketDataDatasetRecoveryFailedEvent? Failure { get { lock (gate) return current?.Failure; } }

    // Called only by the explicitly armed Development verifier. Uses the production deadline and publisher.
    internal void BeginDevelopmentVerification(OptionPipelineInspection inspection, Guid operationId,
        bool holdCompletion, CancellationToken stoppingToken)
    {
        lock (gate)
        {
            if (inspection.ValueDate is null)
                throw new InvalidOperationException("An active option session is required.");
            // A deliberately armed test must also run from an already-failed/stalled starting state.
            // Retire its previous episode without waiting for its work; late completion is identity-fenced.
            if (current is { } previous) _ = previous.Cancellation.CancelAsync();
            var episode = new Episode(inspection, operationId, clock.GetUtcNow() + budget,
                CancellationTokenSource.CreateLinkedTokenSource(stoppingToken)) { HoldCompletion = holdCompletion };
            current = episode;
            episode.Work = Task.Run(() => RunAsync(episode), CancellationToken.None);
        }
    }

    /// <summary>Does not await recovery: a stuck data operation cannot block failure publication.</summary>
    public MarketDataDatasetRecoveryFailedEvent? Observe(CancellationToken stoppingToken)
    {
        var inspection = runtime.Inspect();
        lock (gate)
        {
            if (current is { } previous && previous.Inspection.ValueDate != inspection.ValueDate)
            { previous.Cancellation.Cancel(); current = null; }
            if (inspection.ValueDate is null) return null;
            if (current is null && inspection.Failure is not null)
            {
                var episode = new Episode(inspection, Guid.NewGuid(), clock.GetUtcNow() + budget,
                    CancellationTokenSource.CreateLinkedTokenSource(stoppingToken));
                current = episode;
                episode.Work = Task.Run(() => RunAsync(episode), CancellationToken.None);
            }
            if (current?.State == "Healthy" && inspection.Failure is not null)
            {
                current = null;
                return null; // Start a new failure episode on the next independent observation.
            }
            return current?.Failure;
        }
    }

    async Task RunAsync(Episode episode)
    {
        var remaining = episode.DeadlineUtc - clock.GetUtcNow();
        using var deadline = new CancellationTokenSource(remaining <= TimeSpan.Zero ? TimeSpan.Zero : remaining < budget ? remaining : budget, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(episode.Cancellation.Token, deadline.Token);
        try
        {
            if (remaining <= TimeSpan.Zero) throw new TimeoutException("The two-minute recovery deadline expired before work could start.");
            logger.LogWarning("Option emergency recovery started; OperationId={OperationId}; DeadlineUtc={DeadlineUtc}; Reason={Reason}", episode.Id, episode.DeadlineUtc, episode.Inspection.Failure);
            // Even the synchronous prefix of a faulty adapter must not block the deadline observer.
            var work = Task.Run(() => runtime.RecoverAndVerifyAsync(episode.Id, episode.Inspection, episode.DeadlineUtc, linked.Token), CancellationToken.None);
            // Observe abandoned faults; never await an uncooperative operation beyond the episode deadline.
            _ = work.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            await work.WaitAsync(linked.Token).ConfigureAwait(false);
            if (episode.HoldCompletion)
            {
                logger.LogWarning("Development verification deliberately withholding recovery completion; OperationId={OperationId}", episode.Id);
                await Task.Delay(Timeout.InfiniteTimeSpan, clock, linked.Token).ConfigureAwait(false);
            }
            linked.Token.ThrowIfCancellationRequested();
            lock (gate) if (ReferenceEquals(current, episode)) episode.State = "Healthy";
            logger.LogInformation("Option emergency recovery verified; OperationId={OperationId}", episode.Id);
        }
        catch (OperationCanceledException) when (episode.Cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var id = new MarketDataFeedId(episode.Inspection.ValueDate!.Value);
            var detail = deadline.IsCancellationRequested ? "The two-minute recovery deadline expired." : error.Message;
            var failure = new MarketDataDatasetRecoveryFailedEvent
            {
                Id = Guid.NewGuid(), CommandId = episode.Id, EntityId = id,
                Subject = new(ActorType.Event, MarketDataDatasetRecoveryFailedEvent.Actor, MarketDataDatasetRecoveryFailedEvent.Verb, id.Format()),
                ErrorDate = now, ReceivedOn = now, ErrorCode = 503, ErrorType = ErrorType.System,
                EventSource = nameof(OptionPipelineRecoveryCoordinator), Dataset = "GLBX.MDP3", Role = "Options",
                ErrorData = episode.DeadlineUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                ErrorMessage = $"Option pipeline recovery failed: {detail} Affected positions: {string.Join(", ", episode.Inspection.Positions)}. "
                    + "Live monitoring is not verified. "
                    + (Environment.GetEnvironmentVariable("IFM_API_RECOVERY_CONTROLLER") == "1"
                        ? "Server Manager will check the single-restart allowance after this UI notification."
                        : "Automatic API restart is disabled in this release.")
            };
            lock (gate)
                if (ReferenceEquals(current, episode)) { episode.Failure = failure; episode.State = "Failed"; }
            logger.LogError(error, "Option emergency recovery failed; OperationId={OperationId}; Detail={Detail}", episode.Id, failure.ErrorMessage);
        }
    }

    static TimeSpan Validate(TimeSpan value) => value > TimeSpan.Zero && value <= TimeSpan.FromMinutes(2)
        ? value : throw new ArgumentOutOfRangeException(nameof(value));
    sealed class Episode(OptionPipelineInspection inspection, Guid id, DateTimeOffset deadlineUtc, CancellationTokenSource cancellation)
    {
        public OptionPipelineInspection Inspection { get; } = inspection;
        public Guid Id { get; } = id;
        public DateTimeOffset DeadlineUtc { get; } = deadlineUtc;
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public string State { get; set; } = "Recovering";
        public bool HoldCompletion { get; init; }
        public Task? Work { get; set; }
        public MarketDataDatasetRecoveryFailedEvent? Failure { get; set; }
    }
}
