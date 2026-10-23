using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;

/// <summary>Restores only option market/monitoring processing; financial actors and API lifetime are untouched.</summary>
public sealed class OptionPipelineRecoveryRuntime(IActorSupervisor supervisor, IMarketDataApi market,
    IFuturesMarketSessionAuthority sessions, TradeRealtimeCacheRegistry caches, TimeProvider? timeProvider = null) : IOptionPipelineRecoveryRuntime
{
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    readonly Dictionary<string, DateTimeOffset> missingPlanSince = new(StringComparer.Ordinal);
    DateOnly? observedDate;
    public OptionPipelineInspection Inspect()
    {
        var date = sessions.Current.ActiveValueDate;
        if (observedDate != date) { missingPlanSince.Clear(); observedDate = date; }
        if (date is null || !supervisor.IsReady) return new(null, null, []);
        var now = clock.GetUtcNow();
        var trades = caches.CaptureRecoveryObservations().Where(trade => trade.ValueDate == date
            && trade.Position?.IsOpen == true && trade.Legs.Any(leg => leg.Observation is { } observation
                && now - observation.ObservedAtUtc < TimeSpan.FromMinutes(2))).ToArray();
        var positions = trades.Select(trade => trade.PositionId.Format()).ToArray();
        foreach (var mailbox in Mailboxes())
        {
            if (mailbox.LifecycleState == ActorMailboxLifecycleState.Quarantined
                || !mailbox.IsAdmissionOpen && (mailbox.IsProcessing || mailbox.QueueDepth > 0 || mailbox.LastRejectionReason == "Stopping"))
                return new(date, $"{mailbox.ThreadId}: admission closed or quarantined", positions);
            if (mailbox.IsProcessing && mailbox.LastStartedUtc is { } started
                && now.UtcDateTime - started >= TimeSpan.FromSeconds(10))
                return new(date, $"{mailbox.ThreadId}: handler is not making timely progress", positions);
        }
        foreach (var trade in trades)
        {
            var newest = trade.Legs.Select(leg => leg.Observation?.ObservedAtUtc).Where(at => at.HasValue).Max();
            var key = trade.PositionId.Format();
            if (trade.Plan is null && newest is { } recent && now - recent < TimeSpan.FromSeconds(15))
                missingPlanSince.TryAdd(key, now);
            else missingPlanSince.Remove(key);
            if (newest is { } quote && now - quote < TimeSpan.FromSeconds(15)
                && (trade.Plan is null ? missingPlanSince.TryGetValue(key, out var since) && now - since > TimeSpan.FromSeconds(10)
                    : quote.UtcDateTime - trade.Plan.CalculatedAtUtc > TimeSpan.FromSeconds(10)))
                return new(date, $"{trade.PositionId.Format()}: fresh quotes are not producing current plans", positions);
        }
        return new(date, null, positions);
    }

    IEnumerable<ActorMailboxMetricsSnapshot> Mailboxes() => supervisor.RuntimeContext.CaptureActorMetricsSources()
        .Where(source => source.ActorId.ActorType == ActorType.Realtime
            && source.ActorId.Name is "FuturesOptionRealtime" or "IronCondorTradePositionRealtime")
        .SelectMany(source => source.CaptureSnapshot().Mailboxes);

    public async Task RecoverAndVerifyAsync(Guid operationId, OptionPipelineInspection inspection,
        DateTimeOffset deadlineUtc, CancellationToken cancellationToken)
    {
        var started = clock.GetUtcNow();
        var baseline = caches.CaptureRecoveryObservations().ToDictionary(trade => trade.PositionId.Format(),
            trade => (RouteGeneration: trade.Position?.RouteGeneration ?? 0, PositionSequence: trade.Position?.PositionSequence ?? 0));
        // Whole actor generation replacement abandons pending setup work and detaches active handlers without draining.
        foreach (var name in new[] { "IronCondorTradePositionRealtime", "FuturesOptionRealtime" })
        {
            var id = new ActorMailboxId(ActorType.Realtime, name);
            if (supervisor.ActorExists(id))
                await supervisor.RestartAsync(id, cancellationToken).AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        var status = await market.GetBackendStatusAsync(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
        var options = status.Datasets.SingleOrDefault(dataset => dataset.Role == "Options" && dataset.Dataset == "GLBX.MDP3"
            && dataset.ValueDate == inspection.ValueDate)
            ?? throw new InvalidOperationException("No owned Options/GLBX.MDP3 worker is available for restoration.");
        // Join a Host-owned worker recovery instead of issuing a competing replacement.
        if (options.Lifecycle == "Recovering")
        {
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), clock, cancellationToken).ConfigureAwait(false);
                status = await market.GetBackendStatusAsync(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
                options = status.Datasets.Single(dataset => dataset.Role == "Options" && dataset.Dataset == "GLBX.MDP3");
            } while (options.Lifecycle == "Recovering");
            if (!options.Running || !options.Healthy) throw new InvalidOperationException(options.Error ?? "Host option recovery failed.");
        }
        else
        {
            var oldProcessId = options.ProcessId;
            var recovered = await market.RecoverMarketDataAsync(new(operationId, "Options", options.Dataset,
                options.WorkerInstanceId, options.GenerationId, inspection.ValueDate!.Value,
                "Emergency option pipeline recovery; discard pending setup work and replace the process immediately.",
                ReplaceProcessImmediately: true, DeadlineUtc: deadlineUtc), cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
            if (!recovered.Recovered) throw new InvalidOperationException(recovered.Error ?? "Options worker restoration failed.");
            var replacementStatus = await market.GetBackendStatusAsync(cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
            var replacement = replacementStatus.Datasets.Single(dataset => dataset.Role == "Options" && dataset.Dataset == "GLBX.MDP3");
            if (!replacement.Running || !replacement.Healthy || replacement.ProcessId <= 0 || replacement.ProcessId == oldProcessId)
                throw new InvalidOperationException("Host did not prove immediate options-process replacement; deploy the matching updated Host and workers.");
        }
        if (inspection.Positions.Length == 0) throw new InvalidOperationException("No live position evidence exists to verify end-to-end option recovery.");
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sessions.Current.ActiveValueDate != inspection.ValueDate) throw new OperationCanceledException(cancellationToken);
            var current = caches.CaptureRecoveryObservations();
            var proven = inspection.Positions.All(id => current.Any(trade => trade.PositionId.Format() == id
                && trade.ValueDate == inspection.ValueDate && trade.Position is { } position && trade.Plan is { } plan
                && plan.IronCondorTradePlanSnapshot?.IsComplete == true
                && position.AsOfUtc >= started.UtcDateTime && plan.CalculatedAtUtc >= started.UtcDateTime
                && (plan.Position.RouteGeneration > baseline.GetValueOrDefault(id).RouteGeneration
                    || plan.Position.RouteGeneration == baseline.GetValueOrDefault(id).RouteGeneration
                        && plan.Position.PositionSequence > baseline.GetValueOrDefault(id).PositionSequence)
                && position.RouteGeneration == plan.Position.RouteGeneration
                && position.PositionSequence >= plan.Position.PositionSequence
                && plan.Position.Legs.Length == 4 && plan.Position.Legs.All(leg => leg.OptionTick?.ObservedAtUtc >= started
                    && leg.OptionRisk?.CalculatedAtUtc >= started)
                && trade.Legs.Length == 4 && trade.Legs.All(leg => leg.Observation?.ObservedAtUtc >= started
                    && leg.LastValidRisk?.CalculatedAtUtc >= started)
                && caches.HasPublishedPosition(trade.TradeId, plan.Position.RouteGeneration, plan.Position.PositionSequence)));
            if (proven && Mailboxes().All(mailbox => mailbox.IsAdmissionOpen)) return;
            await Task.Delay(TimeSpan.FromMilliseconds(250), clock, cancellationToken).ConfigureAwait(false);
        }
    }
}
