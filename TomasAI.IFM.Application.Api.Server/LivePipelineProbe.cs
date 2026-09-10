using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Model;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Api.Server;

public sealed class LivePipelineProbe(MarketDataRuntimeHealthCheck feedCheck,
    ActorRuntimeHealthCheck actors, DatabentoMarketDataApi market,
    IFuturesMarketSessionAuthority sessions, IFuturesBarDataTimer bars,
    IMarketDataFeedCommandApi commands, IMarketDataAnalyticsCommandApi analytics, IDbContextFactory db,
    MarketDataOperationsHealthService operations, LivePipelineEvidence evidence,
    DatabentoMarketDataWatchdogService watchdog, TimeProvider time, IActorSupervisor? supervisor = null,
    NatsConnectionManager? messaging = null) : ILivePipelineProbe
{
    readonly Dictionary<string, (ulong Produced, ulong Consumed, long Completed, ulong Ring, int Channel)> previous = new();
    readonly Dictionary<string, (DateTimeOffset Through, DateTime ObservedUtc)> closedWatermarks = new();
    public async Task<LivePipelineHealthSnapshot> CheckAsync(CancellationToken token)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var session = sessions.Current;
        var checks = new List<LivePipelineCheck>();
        void Add(string name, string scope, bool ok, string reason, DateTime? progress = null)
            => checks.Add(new(name, scope, ok ? "Healthy" : "Degraded", reason, now, progress));
        Add("Session authority", "session", session.IsValid && session.NextTransitionUtc > now,
            "Validated session, value date and next boundary.");
        if (!session.IsValid) return Result();
        if (session.ActiveValueDate is not { } date)
        {
            checks.Add(new("Live pipeline", "session", "Inactive", "Planned market closure.", now));
            return Result();
        }
        try
        {
            var runtime = await feedCheck.CheckHealthAsync(new HealthCheckContext(), token).ConfigureAwait(false);
            Add("Databento feed", "datasets", runtime.Status == HealthStatus.Healthy, runtime.Description ?? "No feed diagnosis.");
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        { checks.Add(new("Feed readiness probe", "runtime", "Unknown", ex.Message, now)); }
        var actorHealth = await actors.CheckHealthAsync(new HealthCheckContext(), token).ConfigureAwait(false);
        Add("Actor routing", "runtime", actorHealth.Status == HealthStatus.Healthy, actorHealth.Description ?? "No actor diagnosis.");
        try
        {
            using var messagingDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            messagingDeadline.CancelAfter(TimeSpan.FromSeconds(5));
            var roundTrip = messaging is null ? null : await messaging.ProbeAsync(messagingDeadline.Token).AsTask().WaitAsync(messagingDeadline.Token).ConfigureAwait(false);
            checks.Add(roundTrip is null
                ? new("Messaging transport", "NATS", "Unknown", "The pipeline has no initialized shared NATS connection.", now)
                : new("Messaging transport", "NATS", "Healthy", $"Shared connection PING/PONG completed in {roundTrip.Value.TotalMilliseconds:F0} ms.", now, now));
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        { Add("Messaging transport", "NATS", false, "Shared connection probe failed: " + ex.Message); }
        var health = market.GetHealth();
        var epoch = health.Epoch;
        Add("Value date", "feed", health.ValueDate == date, "Feed epoch must match authoritative active date.");
        if (epoch?.DatasetFeedStatuses is not { Count: > 0 })
            checks.Add(new("Native diagnostics", "datasets", "Unknown", "No per-dataset diagnostic evidence is available.", now));
        foreach (var dataset in epoch?.DatasetFeedStatuses ?? [])
        {
            var native = dataset.Health;
            var aggregation = dataset.AggregationMetrics;
            var key = dataset.Dataset + "/" + dataset.GenerationId;
            var prior = previous.GetValueOrDefault(key);
            Add("Native transport", dataset.Dataset, native.TransportReady && native.TradingReady,
                "Transport/subscription readiness and terminal state.");
            Add("Native delivery", dataset.Dataset,
                !(prior.Ring > 0 && native.RecordsConsumed == prior.Consumed && native.RingUsedRecords > 0),
                "Native producer progress must be followed by drain progress when records are buffered.");
            Add("Aggregation", dataset.Dataset,
                !(prior.Channel > 0 && aggregation.RecordsCompleted == prior.Completed && native.ChannelBatchCount > 0),
                "Managed delivery must be followed by aggregation progress.");
            if (previous.Count > 32) previous.Clear();
            previous[key] = (native.RecordsProduced, native.RecordsConsumed, aggregation.RecordsCompleted, native.RingUsedRecords, native.ChannelBatchCount);
        }
        Add("Lifecycle consistency", "watchdog", !health.Running || watchdog.Current.State is DatabentoLifecycleState.Healthy or DatabentoLifecycleState.Degraded,
            "Watchdog state must agree with the active feed; restarting is not proof of recovery.");
        var central = operations.GetReadModel();
        Add("Operations observer", "runtime", central.SessionState != "Unknown" && now - central.ObservedOnUtc <= TimeSpan.FromSeconds(15),
            "Independent operations observer must supply current evidence.");
        // These stages have explicit worker/processor gauges. Unknown or absent evidence stays unknown.
        foreach (var stage in central.Stages.Where(x => x.Required))
            checks.Add(new("Operations " + stage.Stage, "runtime", stage.Status switch
            { "Green" => "Healthy", "Inactive" => "Unknown", "Red" => "Unhealthy", _ => "Degraded" },
                stage.Reason, stage.LastObservedUtc ?? now, stage.LastSucceededUtc));
        foreach (var stage in central.Stages.Where(x => !x.Required && (x.Received > 0 || x.Completed > 0) && !x.Stage.StartsWith("Databento", StringComparison.Ordinal)))
            checks.Add(new("Analytics/output " + stage.Stage, "runtime",
                stage.LastFailedUtc > stage.LastSucceededUtc ? "Degraded"
                : stage.Received > stage.Completed + stage.Coalesced && stage.LastSucceededUtc is { } last && now - last > TimeSpan.FromMinutes(1) ? "Degraded" : "Healthy",
                "Progress and failures of enabled analytics/output stages.", now, stage.LastSucceededUtc));
        Add("Bar timer", date.ToString("yyyy-MM-dd"), bars.IsRunning(new(date)),
            "The process-local 15-second chart timer must be registered and running.");
        var latest = new Dictionary<string, DateTime>();
        var expectedContracts = new Dictionary<string, string>();
        foreach (var symbol in new[] { "ES", "VX" })
        {
            if (!market.TryGetOnTheRunFuturesContract(symbol, out var contract))
            {
                Add("Contract mapping", symbol, false, "No current contract.");
                continue;
            }
            expectedContracts[symbol] = contract.ContractId;
            var status = epoch?.ContractStatuses?.FirstOrDefault(x => x.ContractId == contract.ContractId);
            var grace = session.IsOffTrading ? TimeSpan.FromMinutes(15) : TimeSpan.FromMinutes(5);
            Add("Price cache", symbol, status?.LastAcceptedCacheUpdateAtUtc is { } accepted && now - accepted.UtcDateTime < grace,
                "Accepted cache update freshness, scoped to current contract.", status?.LastAcceptedCacheUpdateAtUtc?.UtcDateTime);
            Add("Price publication", symbol, status?.LastMarketPricePublishedAtUtc is { } published
                && (status?.LastAcceptedCacheUpdateAtUtc is not { } input || input - published < TimeSpan.FromMinutes(1)),
                "Publication must follow accepted price updates.", status?.LastMarketPricePublishedAtUtc?.UtcDateTime);
            // A read verifies the storage/query path as well as the producer.
            try
            {
                var bar = await db.MarketDataDb.GetLastFuturesBarDataAsync(contract.ContractId, symbol, date).WaitAsync(token).ConfigureAwait(false);
                var valid = bar is not null && bar.ContractId == contract.ContractId && bar.ValueDate == date;
                Add("Chart storage/query", symbol, valid && now - bar!.BarDate < TimeSpan.FromSeconds(45),
                    "Expected a current 15-second chart bar in durable storage.", bar?.BarDate);
                if (valid) latest[symbol] = bar!.BarDate;
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            { Add("Chart storage/query", symbol, false, ex.Message); }
            var tick = evidence.Get("Tick storage", contract.ContractId);
            checks.Add(tick is null ? new("Tick storage", symbol, "Unknown", "No confirmed durable tick write in this process.", now)
                : status?.LastDurableTickPublishedAtUtc is { } sent && tick.LastProgressUtc is { } stored && sent.UtcDateTime - stored > TimeSpan.FromMinutes(1)
                    ? tick with { Status = "Degraded", Reason = "Durable tick publication is ahead of confirmed storage." } : tick);
        }
        if (supervisor is not null)
            Add("ITI route", "ES", supervisor.GetRealtimeRoutes(PriceRoute).Any(x => x.Destination == ItiMailbox), "Current realtime routing table must contain the ITI consumer.");
        else checks.Add(evidence.Get("ITI route", "ES") ?? new("ITI route", "ES", "Unknown", "ITI realtime router attachment is unverified.", now));
        var iti = evidence.Get("ITI", "ES");
        var unprocessedTrade = market.TryGetOnTheRunFuturesContract("ES", out var itiContract)
            && market.TryGetLastTickPrice(itiContract.ContractId, out var itiPrice) && itiPrice.Trade is { } trade
            && iti?.LastProgressUtc is { } evaluated && trade.EventTimestamp.UtcDateTime - evaluated > TimeSpan.FromMinutes(1);
        checks.Add(iti is null ? new("ITI", "ES", "Unknown", "No eligible trade evaluation observed.", now)
            : unprocessedTrade && now - iti.ObservedUtc > TimeSpan.FromMinutes(1)
                ? iti with { Status = "Degraded", Reason = "ES trades advanced without ITI evaluation progress; verify routing and VX prerequisites." } : iti);
        if (market.TryGetOnTheRunFuturesContract("ES", out var es))
        {
            foreach (var activation in FuturesIntradaySignalActivationProfile.Create(es.ContractId, date))
            {
                Add("Analytics attachments", "RSI/" + activation.TimeFrame, FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Snapshot().Contains(activation.Rsi), "Expected RSI bar consumer attachment.");
                Add("Analytics attachments", "ATR/" + activation.TimeFrame, FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.Snapshot().Contains(activation.Atr), "Expected ATR bar consumer attachment.");
                Add("Analytics attachments", "ADX/" + activation.TimeFrame, FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Snapshot().Contains(activation.Adx), "Expected ADX bar consumer attachment.");
                Add("Analytics attachments", "MACD/" + activation.TimeFrame, FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Snapshot().Contains(activation.Macd), "Expected MACD bar consumer attachment.");
                var published = FuturesTradeSessionBarPublicationProgress.Get(es.ContractId, date, activation.TimeFrame);
                var watermarkKey = es.ContractId + "/" + date + "/" + activation.TimeFrame;
                if (closedWatermarks.TryGetValue(watermarkKey, out var closed) && now - closed.ObservedUtc >= TimeSpan.FromSeconds(45))
                {
                    Add("Analytics processing", "RSI/" + activation.TimeFrame, FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.HasProcessed(activation.Rsi, closed.Through), "Published closed bar must reach the RSI command processor.");
                    Add("Analytics processing", "ATR/" + activation.TimeFrame, FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.HasProcessed(activation.Atr, closed.Through), "Published closed bar must reach the ATR command processor.");
                    Add("Analytics processing", "ADX/" + activation.TimeFrame, FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.HasProcessed(activation.Adx, closed.Through), "Published closed bar must reach the ADX command processor.");
                    Add("Analytics processing", "MACD/" + activation.TimeFrame, FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.HasProcessed(activation.Macd, closed.Through), "Published closed bar must reach the MACD command processor.");
                }
                if (published is { } newest)
                {
                    if (closedWatermarks.Count > 128) closedWatermarks.Clear();
                    closedWatermarks[watermarkKey] = (newest.Through, now);
                }
            }
        }
        checks.Add(evidence.Get("Market Outlook inputs", "ES") ?? new("Market Outlook inputs", "ES", "Unknown", "Snapshot input completeness is unverified.", now));
        var outlook = evidence.Get("Market Outlook publication", "ES");
        var outlookBehind = market.TryGetOnTheRunFuturesContract("ES", out var outlookEs)
            && market.TryGetLastTickPrice(outlookEs.ContractId, out var latestEs) && latestEs.Trade is { } latestTrade
            && outlook?.LastProgressUtc is { } outlookTime && latestTrade.EventTimestamp.UtcDateTime - outlookTime > TimeSpan.FromMinutes(1);
        checks.Add(outlook is null ? new("Market Outlook publication", "ES", "Unknown", "No composed snapshot publication observed.", now)
            : outlookBehind ? outlook with { Status = "Degraded", Reason = "ES trades progressed without a composed/published snapshot." } : outlook);
        if (outlook?.LastProgressUtc is { } snapshotTime) latest["Outlook"] = snapshotTime;
        var outlookStored = evidence.Get("Market Outlook storage", "ES");
        checks.Add(outlookStored is null ? new("Market Outlook storage", "ES", "Unknown", "No persisted restart snapshot confirmed.", now)
            : outlook?.LastProgressUtc is { } outputTime && outlookStored.LastProgressUtc is { } storageTime && outputTime - storageTime > TimeSpan.FromMinutes(1)
                ? outlookStored with { Status = "Degraded", Reason = "Published snapshots are ahead of persistence." } : outlookStored);
        if (expectedContracts.TryGetValue("ES", out var outlookContract)) expectedContracts["Outlook"] = outlookContract;
        checks.AddRange(evidence.CheckUi(date, latest, expectedContracts));
        return Result();

        LivePipelineHealthSnapshot Result() => new(now, session.ActiveValueDate,
            checks.Any(x => x.Required && x.Status == "Unhealthy") ? "Unhealthy"
                : checks.Any(x => x.Required && x.Status == "Degraded") ? "Degraded"
                : checks.Any(x => x.Required && x.Status == "Unknown") ? "Unknown" : "Healthy", checks);
    }

    static readonly ActorTypeId PriceRoute = new(ActorType.Realtime, FuturesMarketPriceUpdatedRealtimeEvent.Actor, FuturesMarketPriceUpdatedRealtimeEvent.Verb);
    static readonly ActorMailboxId ItiMailbox = new(ActorType.Realtime, FuturesItiSignalRealtimeActor.ActorName);
    public bool CanRecover(LivePipelineCheck failure) => failure.Component is "Databento feed" or "Native transport"
        or "Native delivery" or "Aggregation" or "Lifecycle consistency" or "Analytics attachments" or "Bar timer" or "ITI route";

    public async Task RecoverAsync(LivePipelineCheck failure, CancellationToken token)
    {
        if (sessions.Current.ActiveValueDate is not { } date) return;
        if (failure.Component == "ITI route" && supervisor is not null)
        {
            if (!supervisor.ActorExists(ItiMailbox)) await supervisor.StartAsync(ItiMailbox, token);
            supervisor.AddRealtimeRouter(PriceRoute, ItiMailbox);
            return;
        }
        if (failure.Component is "Databento feed" or "Native transport" or "Native delivery" or "Aggregation" or "Lifecycle consistency")
        {
            await watchdog.ProbeAsync(token).ConfigureAwait(false);
            return;
        }
        if (failure.Component == "Analytics attachments" && market.TryGetOnTheRunFuturesContract("ES", out var es))
        {
            foreach (var a in FuturesIntradaySignalActivationProfile.Create(es.ContractId, date))
            {
                if (!FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Snapshot().Contains(a.Rsi)) await analytics.StartFuturesRsiSignalAsync(a.Rsi).WaitAsync(token);
                if (!FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.Snapshot().Contains(a.Atr)) await analytics.StartFuturesAtrSignalAsync(a.Atr).WaitAsync(token);
                if (!FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Snapshot().Contains(a.Adx)) await analytics.StartFuturesAdxSignalAsync(a.Adx).WaitAsync(token);
                if (!FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Snapshot().Contains(a.Macd)) await analytics.StartFuturesMacdSignalAsync(a.Macd).WaitAsync(token);
            }
        }
        if (failure.Component == "Bar timer")
        {
            var contracts = new[] { "ES", "VX" }.Select(symbol => market.TryGetOnTheRunFuturesContract(symbol, out var contract)
                ? contract : null).Where(x => x is not null).ToArray();
            if (contracts.Length != 2) return;
            var result = await commands.StartFuturesBarDataStreamingAsync(contracts!, date).WaitAsync(token).ConfigureAwait(false);
            if (!result.Success) throw new InvalidOperationException(result.ErrorMessage);
        }
    }
}

public sealed class LivePipelineHealthCheck(LivePipelineMonitor monitor) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var snapshot = monitor.Current;
        var data = new Dictionary<string, object> { ["pipeline"] = snapshot };
        return Task.FromResult(snapshot.Status == "Healthy" ? HealthCheckResult.Healthy("All required live-pipeline components verified; UI session evidence is reported separately.", data)
            : HealthCheckResult.Degraded("Live pipeline has failed or unverified components.", data: data));
    }
}
