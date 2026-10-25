using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Futures.Option.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery;

/// <summary>Restores only option market/monitoring processing; financial actors and API lifetime are untouched.</summary>
public sealed class OptionPipelineRecoveryRuntime(IActorSupervisor supervisor, IMarketDataApi market,
    IFuturesMarketSessionAuthority sessions, TradeRealtimeCacheRegistry caches, TimeProvider? timeProvider = null,
    TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingHealth? processingHealth = null,
    ILogger<OptionPipelineRecoveryRuntime>? logger = null) : IOptionPipelineRecoveryRuntime
{
    string? recoveryDetail;
    public string? RecoveryDetail => Volatile.Read(ref recoveryDetail);
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    readonly Dictionary<string, DateTimeOffset> missingPlanSince = new(StringComparer.Ordinal);
    DateOnly? observedDate;
    DateTimeOffset verifiedThrough;
    readonly Dictionary<string, DateTimeOffset> monitoringSince = new(StringComparer.Ordinal);
    public OptionPipelineInspection Inspect()
    {
        try { return InspectCore(); }
        catch (Exception error)
        {
            var date = observedDate ?? sessions.Current.ActiveValueDate;
            return new(date, "Option recovery inspection failed: " + error, caches.CaptureRecoveryObservations()
                .Where(t => t.ValueDate == date && t.Position?.IsOpen == true).Select(t => t.PositionId.Format()).ToArray());
        }
    }

    OptionPipelineInspection InspectCore()
    {
        var session = sessions.Current;
        var date = session.ActiveValueDate;
        if (observedDate != date) { missingPlanSince.Clear(); monitoringSince.Clear(); verifiedThrough = default; observedDate = date; }
        if (date is null || !supervisor.IsReady) return new(null, null, []);
        var now = clock.GetUtcNow();
        var backend = market.GetRuntimeStatus() ?? new TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels.MarketDataFeedRuntimeStatusReadModel();
        var trades = caches.CaptureRecoveryObservations().Where(trade => trade.ValueDate == date
            && trade.Position?.IsOpen == true && (backend.EnabledOptionTrades is null
                || backend.EnabledOptionTrades.Contains(trade.TradeId.Format(), StringComparer.Ordinal))).ToArray();
        var positions = trades.Select(trade => trade.PositionId.Format()).ToArray();
        foreach (var inactive in monitoringSince.Keys.Where(key => !positions.Contains(key, StringComparer.Ordinal)).ToArray())
        { monitoringSince.Remove(inactive); missingPlanSince.Remove(inactive); }
        if (backend.OptionProcessingError is { } failure && backend.OptionProcessingFailureUtc > verifiedThrough)
            return new(date, failure, positions, backend.OptionRecoveryDeadlineUtc);
        if (processingHealth?.Read(date.Value, backend.OptionGenerationId) is { } local && local.ObservedAtUtc > verifiedThrough)
            return new(date, local.Component + ": " + local.Detail, positions);
        foreach (var mailbox in Mailboxes())
        {
            if (mailbox.LifecycleState == ActorMailboxLifecycleState.Quarantined
                || !mailbox.IsAdmissionOpen && (mailbox.IsProcessing || mailbox.QueueDepth > 0 || mailbox.LastRejectionReason == "Stopping"))
                return new(date, $"{mailbox.ThreadId}: admission closed or quarantined", positions);
            if (mailbox.IsProcessing && mailbox.LastStartedUtc is { } started
                && now.UtcDateTime - started >= TimeSpan.FromSeconds(10))
                return new(date, $"{mailbox.ThreadId}: handler is not making timely progress", positions);
        }
        foreach (var actor in supervisor.RuntimeContext.CaptureActorMetricsSources().Where(source =>
            source.ActorId.Name is "FuturesOptionRealtime" or "IronCondorTradePositionRealtime" or "IronCondorForwardPriceFunction" or "IronCondorTradePlanFunction"))
            foreach (var mailbox in actor.CaptureSnapshot().Mailboxes)
                if (positions.Length > 0 && mailbox.LastFailedUtc is { } failed && failed > verifiedThrough.UtcDateTime && failed >= session.SessionStartUtc)
                    return new(date, $"{mailbox.ThreadId}: {mailbox.LastExceptionType}: {mailbox.LastError}", positions);
        foreach (var trade in trades)
        {
            var newest = trade.Legs.Select(leg => leg.Observation?.ObservedAtUtc).Where(at => at.HasValue).Max();
            var key = trade.PositionId.Format();
            monitoringSince.TryAdd(key, now);
            // Missing data stays in inspection; it is not evidence that monitoring was disabled.
            if (now - monitoringSince[key] > TimeSpan.FromSeconds(30) && trade.Legs.Any(leg =>
                leg.Observation is null || now - leg.Observation.ObservedAtUtc > TimeSpan.FromSeconds(30)))
                return new(date, $"{key}: a required option leg has no current quote progress", positions);
            if (now - monitoringSince[key] > TimeSpan.FromSeconds(30) && trade.Legs.Any(leg =>
                leg.LastValidRisk is null || now - leg.LastValidRisk.CalculatedAtUtc > TimeSpan.FromSeconds(30)))
                return new(date, $"{key}: a required option leg has no current qualified pricing progress", positions);
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
        Volatile.Write(ref recoveryDetail, "Replacing option processing actors.");
        var baseline = caches.CaptureRecoveryObservations().ToDictionary(trade => trade.PositionId.Format(),
            trade => (RouteGeneration: trade.Position?.RouteGeneration ?? 0, PositionSequence: trade.Position?.PositionSequence ?? 0));
        // Whole actor generation replacement abandons pending setup work and detaches active handlers without draining.
        foreach (var name in new[] { "IronCondorTradePositionRealtime", "FuturesOptionRealtime" })
        {
            var id = new ActorMailboxId(ActorType.Realtime, name);
            if (supervisor.ActorExists(id))
                await supervisor.RestartAsync(id, cancellationToken).AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        Volatile.Write(ref recoveryDetail, "Restoring the Options worker and saved subscriptions.");
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
            options = replacement;
        }
        if (inspection.Positions.Length == 0) throw new InvalidOperationException("No live position evidence exists to verify end-to-end option recovery.");
        var verificationStarted = clock.GetUtcNow();
        var repairAttempted = new HashSet<string>(StringComparer.Ordinal);
        var lastDiagnostic = DateTimeOffset.MinValue;
        string? repairDetail = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (sessions.Current.ActiveValueDate != inspection.ValueDate) throw new OperationCanceledException(cancellationToken);
            var current = caches.CaptureRecoveryObservations();
            var missing = current.Where(trade => inspection.Positions.Contains(trade.PositionId.Format(), StringComparer.Ordinal))
                .SelectMany(trade => trade.Legs).Where(leg => leg.Observation?.ObservedAtUtc >= started != true
                    || leg.LastValidRisk?.CalculatedAtUtc >= started != true
                    || leg.Observation?.SourceGeneration != options.GenerationId || leg.LastValidRisk?.SourceGeneration != options.GenerationId).ToArray();
            var detail = "Waiting for four fresh legs, a complete newer plan, publication, and open mailboxes. "
                + string.Join("; ", missing.Select(leg => $"Contract={leg.Definition.ContractId}, Quote={leg.Observation?.ObservedAtUtc:O}, Risk={leg.LastValidRisk?.CalculatedAtUtc:O}, Generation={leg.Observation?.SourceGeneration}"))
                + (repairDetail is null ? "" : " Repair: " + repairDetail);
            Volatile.Write(ref recoveryDetail, detail);
            if (clock.GetUtcNow() - lastDiagnostic >= TimeSpan.FromSeconds(5))
            {
                logger?.LogInformation("Option recovery awaiting proof; OperationId={OperationId}; Generation={Generation}; DeadlineUtc={DeadlineUtc}; Detail={Detail}",
                    operationId, options.GenerationId, deadlineUtc, detail);
                lastDiagnostic = clock.GetUtcNow();
            }
            var contracts = missing.Select(leg => leg.Definition.ContractId).Distinct(StringComparer.Ordinal)
                .Where(contract => !repairAttempted.Contains(contract)).Take(64).ToArray();
            if (contracts.Length > 0 && clock.GetUtcNow() - verificationStarted >= TimeSpan.FromSeconds(10))
            {
                foreach (var contract in contracts) repairAttempted.Add(contract);
                try
                {
                    var repaired = await market.RepairOptionObservationsAsync(new(operationId, inspection.ValueDate!.Value,
                        options.GenerationId, contracts, deadlineUtc), cancellationToken).WaitAsync(cancellationToken).ConfigureAwait(false);
                    repairDetail = "Reconnected=" + string.Join(",", repaired.Reconnected) + "; Errors=" + string.Join(";", repaired.Errors);
                }
                catch (Exception error) when (!cancellationToken.IsCancellationRequested)
                { repairDetail = error.Message; logger?.LogWarning(error, "Option missing-leg repair failed; continuing original recovery; OperationId={OperationId}", operationId); }
                continue;
            }
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
                    && leg.OptionRisk?.CalculatedAtUtc >= started && leg.OptionTick.SourceGeneration == options.GenerationId
                    && leg.OptionRisk.SourceGeneration == options.GenerationId)
                && trade.Legs.Length == 4 && trade.Legs.All(leg => leg.Observation?.ObservedAtUtc >= started
                    && leg.LastValidRisk?.CalculatedAtUtc >= started && leg.Observation.SourceGeneration == options.GenerationId
                    && leg.LastValidRisk.SourceGeneration == options.GenerationId)
                && caches.HasPublishedPosition(trade.TradeId, plan.Position.RouteGeneration, plan.Position.PositionSequence)));
            if (proven && Mailboxes().All(mailbox => mailbox.IsAdmissionOpen))
            {
                var runtime = market.GetRuntimeStatus();
                if (runtime?.OptionProcessingFailureUtc > started) throw new InvalidOperationException(runtime.OptionProcessingError);
                if (processingHealth?.Read(inspection.ValueDate!.Value, runtime?.OptionGenerationId ?? Guid.Empty) is { } pending && pending.ObservedAtUtc > started)
                    throw new InvalidOperationException(pending.Detail);
                cancellationToken.ThrowIfCancellationRequested();
                verifiedThrough = clock.GetUtcNow();
                processingHealth?.Recovered(inspection.ValueDate!.Value, runtime?.OptionGenerationId ?? Guid.Empty, started);
                Volatile.Write(ref recoveryDetail, null);
                return;
            }
            await Task.Delay(TimeSpan.FromMilliseconds(250), clock, cancellationToken).ConfigureAwait(false);
        }
    }
}
