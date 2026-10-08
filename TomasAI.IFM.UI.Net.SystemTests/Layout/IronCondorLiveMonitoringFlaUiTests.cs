using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit.Abstractions;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

/// <summary>Enables the real development feed test only when an operator explicitly selects a running UI and trade.</summary>
public sealed class IronCondorLiveMonitoringFactAttribute : FactAttribute
{
    /// <summary>Requires explicit opt-in because this test opens real Databento subscriptions.</summary>
    public IronCondorLiveMonitoringFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("IFM_RUN_IRON_CONDOR_LIVE_MONITORING") != "1")
            Skip = "Set IFM_RUN_IRON_CONDOR_LIVE_MONITORING=1, IFM_LIVE_MONITORING_UI_PID and IFM_LIVE_MONITORING_TRADE.";
    }
}

[CollectionDefinition("Iron Condor live monitoring", DisableParallelization = true)]
public sealed class IronCondorLiveMonitoringCollection;

/// <summary>Exercises the actual running development UI, NATS actors, Databento and TradePlanDb without submitting orders.</summary>
[Collection("Iron Condor live monitoring")]
public sealed class IronCondorLiveMonitoringFlaUiTests(ITestOutputHelper output)
{
    /// <summary>Proves exact leg subscriptions, live observations, position/source events, stored plans and visible monitoring.</summary>
    [IronCondorLiveMonitoringFact]
    public async Task Live_feed_produces_persisted_plan_visible_in_original_trade_view()
    {
        var pid = int.Parse(Required("IFM_LIVE_MONITORING_UI_PID"), CultureInfo.InvariantCulture);
        var tradeId = TradeEntityId.Parse(Required("IFM_LIVE_MONITORING_TRADE"));
        var setupId = Required("IFM_LIVE_MONITORING_SETUP_TRADE");
        var runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var evidence = Path.GetFullPath(Environment.GetEnvironmentVariable("IFM_LIVE_MONITORING_EVIDENCE")
            ?? Path.Combine(".artifacts", "iron-condor-live-monitoring", runId));
        Directory.CreateDirectory(evidence);
        var timeline = new ConcurrentQueue<object>();
        var evidenceLock = new object();
        void Record(string stage, object detail)
        {
            timeline.Enqueue(new { AtUtc = DateTime.UtcNow, Stage = stage, Detail = detail });
            output.WriteLine($"{DateTime.UtcNow:O} {stage}: {JsonSerializer.Serialize(detail)}");
            lock (evidenceLock) File.AppendAllText(Path.Combine(evidence, "events.ndjson"), JsonSerializer.Serialize(new { AtUtc = DateTime.UtcNow, Stage = stage, Detail = detail }) + Environment.NewLine);
        }
        var nats = Environment.GetEnvironmentVariable("IFM_LIVE_MONITORING_NATS") ?? "nats://localhost:4222";
        await using var manager = new NatsConnectionManager();
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = nats }, NullLogger.Instance, manager);
        await producer.StartAsync(new(ActorType.Query, $"LiveMonitoringTest.{runId}"));
        var established = await new EstablishedTradeQueryApi(producer).GetAsync(tradeId, TradeStrategyKind.IronCondor);
        Assert.True(established.Success, established.ErrorMessage);
        var trade = Assert.IsType<EstablishedTradeDefinition>(established.Value);
        Assert.Equal(EstablishedTradeStatus.Open, trade.Status);
        var contracts = trade.Legs.Select(leg => leg.ContractId).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(4, contracts.Count);
        Assert.DoesNotContain("", contracts);
        Record("EstablishedTrade", new { Trade = tradeId.Format(), Contracts = contracts });
        var subscriptions = new ConcurrentDictionary<string, FuturesOptionTickDataStreamingStartedCompleteEvent>();
        var ticks = new ConcurrentDictionary<string, OptionTradeTickPriceDataUpdatedEvent>();
        var positions = new ConcurrentDictionary<Guid, IronCondorPositionChangedEvent>();
        var positionIds = new ConcurrentQueue<Guid>();
        var stopped = new ConcurrentDictionary<string, FuturesOptionTickDataStreamingStoppedCompleteEvent>();
        var plans = new ConcurrentQueue<IronCondorTradePlanUpdatedEvent>();
        var failures = new ConcurrentQueue<string>();
        var firstPricedRecorded = 0;
        var startedAt = DateTime.UtcNow;
        var listener = new NatsActorEventListener(new NatsEventListenerOptions { Url = nats }, NullLogger.Instance, manager);
        await listener.StartAsync($"LiveMonitoringEvidence.{runId}", new Dictionary<ActorMailboxId, List<string>>
        {
            [new(ActorType.Event, FuturesOptionTickDataStreamingStartedCompleteEvent.Actor)] =
                [FuturesOptionTickDataStreamingStartedCompleteEvent.Verb, FuturesOptionTickDataStreamingStartedFailEvent.Verb, FuturesOptionTickDataStreamingStoppedCompleteEvent.Verb],
            [new(ActorType.Notify, OptionTradeTickPriceDataUpdatedEvent.Actor)] = [OptionTradeTickPriceDataUpdatedEvent.Verb],
            [new(ActorType.Event, "FuturesIronCondorTradePositionEvent")] = [IronCondorPositionChangedEvent.Verb],
            [new(ActorType.Event, UpdateIronCondorTradePlanCommand.Actor)] = [IronCondorTradePlanUpdatedEvent.Verb]
        }, (verb, message) =>
        {
            try
            {
                switch (verb)
                {
                    case FuturesOptionTickDataStreamingStartedCompleteEvent.Verb:
                        var subscription = message.AsEvent<FuturesOptionTickDataStreamingStartedCompleteEvent>()!;
                        if (contracts.Contains(subscription.Contract.ContractId) && subscriptions.TryAdd(subscription.Contract.ContractId, subscription))
                            Record("SubscriptionReady", new { subscription.EntityId, subscription.CommandId, subscription.Contract.ContractId });
                        break;
                    case FuturesOptionTickDataStreamingStoppedCompleteEvent.Verb:
                        var released = message.AsEvent<FuturesOptionTickDataStreamingStoppedCompleteEvent>()!;
                        if (subscriptions.TryGetValue(released.ContractId, out var attached)
                            && released.EntityId.MonitoringOwnerId == attached.EntityId.MonitoringOwnerId)
                            stopped[released.ContractId] = released;
                        break;
                    case FuturesOptionTickDataStreamingStartedFailEvent.Verb:
                        var failure = message.AsEvent<FuturesOptionTickDataStreamingStartedFailEvent>()!;
                        if (contracts.Contains(failure.EntityId.ContractId)) { failures.Enqueue(failure.ErrorMessage); Record("SubscriptionFailed", failure.ErrorMessage); }
                        break;
                    case OptionTradeTickPriceDataUpdatedEvent.Verb:
                        var tick = message.AsEvent<OptionTradeTickPriceDataUpdatedEvent>()!;
                        if (contracts.Contains(tick.OptionTickData.ContractId))
                        {
                            if (!ticks.ContainsKey(tick.OptionTickData.ContractId)) Record("FirstLiveLegObservation", tick);
                            ticks[tick.OptionTickData.ContractId] = tick;
                        }
                        break;
                    case IronCondorPositionChangedEvent.Verb:
                        var position = message.AsEvent<IronCondorPositionChangedEvent>()!;
                        if (position.EntityId.Trade == tradeId)
                        {
                            if (positions.TryAdd(position.Id, position)) positionIds.Enqueue(position.Id);
                            while (positions.Count > 4096 && positionIds.TryDequeue(out var retiredPosition)) positions.TryRemove(retiredPosition, out _);
                            if (positions.Count <= 4) Record("PositionChanged", new { position.Id, position.EventId, position.CommandId,
                                position.PositionSnapshot.PositionSequence, position.PositionSnapshot.AsOfUtc });
                        }
                        break;
                    case IronCondorTradePlanUpdatedEvent.Verb:
                        var plan = message.AsEvent<IronCondorTradePlanUpdatedEvent>()!;
                        if (plan.EntityId.Position.Trade == tradeId)
                        {
                            plans.Enqueue(plan);
                            while (plans.Count > 4096) plans.TryDequeue(out _);
                            if (plans.Count <= 4) Record("PlanSourcePublished", new { plan.Id, plan.EventId, plan.SourceEventId,
                                plan.Plan.PlanRevision, plan.Plan.CalculatedAtUtc, plan.Plan.ContentHash });
                            if (plan.Plan.IronCondorTradePlanSnapshot?.IronCondorTradePlanInputs?.CalculatedSpreadPrices is { } calculated
                                && Interlocked.CompareExchange(ref firstPricedRecorded, 1, 0) == 0)
                                Record("FirstPricedPlan", new { plan.Id, plan.EventId, plan.Plan.PlanRevision, calculated });
                        }
                        break;
                }
            }
            catch (Exception error) { failures.Enqueue(error.ToString()); }
            return ValueTask.CompletedTask;
        });
        using var automation = new UIA3Automation();
        automation.ConnectionTimeout = TimeSpan.FromSeconds(5);
        automation.TransactionTimeout = TimeSpan.FromSeconds(5);
        AutomationElement? view = null;
        var stage = "LoadTradeView";
        try
        {
            var main = await WaitElementAsync(() => automation.GetDesktop().FindFirstChild(cf => cf.ByProcessId(pid)), TimeSpan.FromSeconds(15));
            main.AsWindow().SetForeground();
            view = main.FindFirstDescendant(cf => cf.ByAutomationId("IronCondorTradeView"));
            if (view is null)
            {
                var editor = main.FindFirstDescendant(cf => cf.ByAutomationId("TradeOrderEditorForm"));
                if (editor is null)
                {
                    var open = main.FindFirstDescendant(cf => cf.ByName("Trade Orders").And(cf.ByControlType(ControlType.Button))).AsButton();
                    // A synchronous UIA Invoke enters ShowDialog and blocks later automation calls. Send the native
                    // button click asynchronously to this FlaUI-discovered control; the actual UI handler opens it.
                    ClickModalButton(open);
                    editor = await WaitElementAsync(() => main.FindFirstDescendant(cf => cf.ByAutomationId("TradeOrderEditorForm")), TimeSpan.FromSeconds(30));
                }
                var list = editor.FindFirstDescendant(cf => cf.ByAutomationId("lstTrades"));
                Assert.Contains("Open", list.Name);
                var row = list.FindFirstDescendant(cf => cf.ByName(setupId).And(cf.ByControlType(ControlType.ListItem)));
                Assert.NotNull(row);
                row.AsListBoxItem().Select();
                ClickModalButton(editor.FindFirstDescendant(cf => cf.ByAutomationId("btnLoadOrder")));
                view = await WaitElementAsync(() => main.FindFirstDescendant(cf => cf.ByAutomationId("IronCondorTradeView")), TimeSpan.FromSeconds(30));
            }
            Assert.Contains(tradeId.Format(), view.Name);
            var feed = view.FindFirstDescendant(cf => cf.ByAutomationId("ddlLiveFeed")).AsComboBox();
            Assert.True(feed.IsEnabled);
            Assert.Contains("OFF", feed.Name);
            FlaUI.Core.Capturing.Capture.Element(view).ToFile(Path.Combine(evidence, "before.png"));
            Record(stage, new { view.Name, Feed = feed.Name });
            // Resolve native controls before high-frequency plan rows populate the view's accessibility tree.
            var price = view.FindFirstDescendant(cf => cf.ByAutomationId("txtRtNetSpread"));
            var graph = view.FindFirstDescendant(cf => cf.ByAutomationId("graphSpreadDistribution"));
            var listPlan = view.FindFirstDescendant(cf => cf.ByAutomationId("lstTradePlanAction"));
            var legControls = trade.Legs.ToDictionary(leg => leg.ContractId, leg =>
            {
                var prefix = (leg.PutCall == 1 ? "txtCall" : "txtPut") + (leg.SignedQuantity > 0 ? "Long" : "Short");
                return (Bid: view.FindFirstDescendant(cf => cf.ByAutomationId(prefix + "Bid")).AsTextBox(),
                    Ask: view.FindFirstDescendant(cf => cf.ByAutomationId(prefix + "Ask")).AsTextBox());
            });
            startedAt = DateTime.UtcNow;
            SelectCombo(feed, "LiveFeed ON", automation);
            stage = "FourSubscriptions";
            await WaitAsync(() =>
            {
                if (subscriptions.Count == 4) return true;
                var dialog = main.FindFirstChild(cf => cf.ByControlType(ControlType.Window).And(cf.ByName("Iron Condor Live Feed Error")));
                if (dialog is not null)
                {
                    var message = string.Join("; ", dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)).Select(value => value.Name));
                    Record("VisibleStartupError", message);
                    dialog.FindFirstDescendant(cf => cf.ByName("OK").And(cf.ByControlType(ControlType.Button))).AsButton().Click();
                    throw new InvalidOperationException(message);
                }
                return subscriptions.Count == 4;
            }, TimeSpan.FromSeconds(45), failures);
            Assert.Equal(4, subscriptions.Values.Select(value => value.EntityId).Distinct().Count());
            stage = "FourLiveLegObservations";
            await WaitAsync(() => ticks.Count == 4, TimeSpan.FromSeconds(90), failures);
            stage = "PositionAndPlanSource";
            IronCondorTradePlanUpdatedEvent? observed = null;
            await WaitAsync(() =>
            {
                observed = plans.LastOrDefault(plan => plan.EventId > 0 && plan.Plan.CalculatedAtUtc >= startedAt
                    && positions.ContainsKey(plan.SourceEventId)
                    && plan.Plan.Position.Legs.All(leg => leg.LastPriceAtUtc >= startedAt));
                return observed is not null;
            }, TimeSpan.FromSeconds(45), failures);
            Assert.NotNull(observed);
            Assert.True(observed.EventId > 0, "The source event must have a committed event-log ID.");
            Assert.NotEqual(Guid.Empty, observed.CommandId);
            Assert.NotNull(observed.Plan.IronCondorTradePlanSnapshot);
            stage = "SourceEventLog";
            var committedSource = await ReadSourceAsync(observed.EventId);
            Assert.Equal(observed.Id, committedSource.Id);
            Assert.Equal(observed.CommandId, committedSource.CommandId);
            Assert.Equal(observed.Plan.ContentHash, committedSource.Plan.ContentHash);
            Assert.NotNull(committedSource.Plan.IronCondorTradePlanSnapshot);
            Record(stage, new { committedSource.Id, committedSource.EventId, committedSource.CommandId, committedSource.SourceEventId,
                committedSource.Plan.ContentHash });
            stage = "QualifiedOptionCalculator";
            IronCondorTradePlanUpdatedEvent? pricedPlan = null;
            await WaitAsync(() =>
            {
                pricedPlan = plans.LastOrDefault(plan => plan.Plan.IronCondorTradePlanSnapshot?.IronCondorTradePlanInputs?.CalculatedSpreadPrices is { } prices
                    && prices.OptionLegPrices.Length == 4 && prices.ValidUntilUtc > DateTime.UtcNow);
                return pricedPlan is not null;
            }, TimeSpan.FromSeconds(45), failures);
            Assert.NotNull(pricedPlan);
            Record(stage, pricedPlan.Plan.IronCondorTradePlanSnapshot!.IronCondorTradePlanInputs!.CalculatedSpreadPrices!);
            stage = "MonitoringBeyondOriginalLeaseExpiry";
            var renewalCheckAt = startedAt.AddSeconds(70);
            await WaitAsync(() => DateTime.UtcNow >= renewalCheckAt && plans.Any(plan =>
                plan.Plan.CalculatedAtUtc >= renewalCheckAt
                && plan.Plan.IronCondorTradePlanSnapshot?.IronCondorTradePlanInputs?.CalculatedSpreadPrices is { } renewedPrices
                && renewedPrices.ValidUntilUtc > DateTime.UtcNow), TimeSpan.FromSeconds(90), failures);
            Assert.Contains("ON", feed.Name);
            Record(stage, new { ElapsedSeconds = (DateTime.UtcNow-startedAt).TotalSeconds });
            var query = new StrategyTradePlanQueryApi(producer);
            StrategyTradePlanSnapshot? stored = null;
            stage = "TradePlanDbProjection";
            await WaitAsync(async () =>
            {
                using var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var result = await query.GetCurrentIronCondorAsync(observed.EntityId.Position, observed.EntityId.ValueDate, readTimeout.Token);
                if (!result.Success) Record("ProjectionQueryFailure", result.ErrorMessage);
                stored = result.Value;
                return result.Success && stored is not null && stored.CalculatedAtUtc >= renewalCheckAt
                    && stored.IronCondorTradePlanSnapshot?.IronCondorTradePlanInputs?.CalculatedSpreadPrices is not null
                    && plans.Any(plan => plan.Plan.ContentHash == stored.ContentHash);
            }, TimeSpan.FromSeconds(30));
            var storedSource = plans.Last(plan => plan.Plan.ContentHash == stored!.ContentHash);
            var committedStoredSource = await ReadSourceAsync(storedSource.EventId);
            Assert.Equal(stored!.ContentHash, committedStoredSource.Plan.ContentHash);
            Record(stage, new { Stored = stored, SourceEventId = storedSource.EventId,
                PositionAgeSeconds = (stored!.CalculatedAtUtc-stored.Position.AsOfUtc).TotalSeconds });
            Assert.InRange((stored.CalculatedAtUtc-stored.Position.AsOfUtc).TotalSeconds, 0, stored.Parameters.MaximumDataAgeSeconds);
            stage = "VisibleLivePrices";
            await WaitAsync(() => positions.Values.Any(position =>
            {
                var quantity = position.PositionSnapshot.Legs.Min(leg => Math.Abs(leg.SignedQuantity));
                var mark = position.PositionSnapshot.Legs.Sum(leg => leg.CurrentPrice * leg.SignedQuantity) / quantity;
                return price.AsTextBox().Text == mark.ToString("0.00", CultureInfo.CurrentCulture)
                    && graph.Name == $"Iron Condor observed spread price {mark:0.00}; position {position.PositionSnapshot.PositionSequence}";
            }), TimeSpan.FromSeconds(20), failures);
            foreach (var leg in trade.Legs)
            {
                await WaitAsync(() =>
                {
                    var quote = ticks[leg.ContractId].OptionTickData;
                    return legControls[leg.ContractId].Bid.Text == quote.BidPrice.ToString("0.00", CultureInfo.CurrentCulture)
                        && legControls[leg.ContractId].Ask.Text == quote.AskPrice.ToString("0.00", CultureInfo.CurrentCulture);
                }, TimeSpan.FromSeconds(10), failures);
            }
            Record(stage, new { ObservedSpread = price.AsTextBox().Text, Graph = graph.Name });
            Assert.False(graph.IsOffscreen, "The observed spread graph must be visible in the trade view.");
            stage = "VisiblePlan";
            string[]? visiblePlan = null;
            var movingRowReads = 0;
            await WaitAsync(() =>
            {
                try
                {
                    // Only read identity and the calculator price. UIA row objects can move when a new
                    // snapshot is inserted; reading the whole history creates a stale accessibility target.
                    var rows = listPlan.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem)).Take(10)
                        .Select(row =>
                        {
                            var cells = row.FindAllChildren();
                            return cells.Length < 37 ? [] : cells.Take(4).Select(cell => cell.Name)
                                .Append(cells[36].Name).ToArray();
                        }).ToArray();
                    // Capture notifications after the UI read, so an event that arrived while the rows
                    // were being read can match its visible revision.
                    var known = plans.Where(plan => plan.Plan.IronCondorTradePlanSnapshot?.IronCondorTradePlanInputs?.CalculatedSpreadPrices is not null)
                        .GroupBy(plan => plan.Plan.PlanRevision).ToDictionary(group => group.Key, group => group.Last());
                    visiblePlan = rows.FirstOrDefault(values => values.Length == 5
                        && values[0] == tradeId.OrderId.ToString(CultureInfo.InvariantCulture)
                        && values[1] == tradeId.TradeId.ToString(CultureInfo.InvariantCulture)
                        && long.TryParse(values[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var revision)
                        && known.TryGetValue(revision, out var source)
                        && values[4] == source.Plan.IronCondorTradePlanSnapshot!.NetPrice?.ToString(CultureInfo.InvariantCulture));
                    return visiblePlan is not null;
                }
                catch (COMException error)
                {
                    if (movingRowReads++ < 2) Record("MovingPlanRow", new { error.HResult, error.Message });
                    return false;
                }
            }, TimeSpan.FromSeconds(20), failures);
            var headers = listPlan.FindAllDescendants(cf => cf.ByControlType(ControlType.HeaderItem));
            Assert.Equal(48, headers.Length);
            Record(stage, new { OrderId = visiblePlan![0], TradeId = visiblePlan[1], ValueDate = visiblePlan[2],
                PlanRevision = visiblePlan[3], CalculatedNetPrice = visiblePlan[4], MovingRowReads = movingRowReads });
            File.WriteAllLines(Path.Combine(evidence, "visible-plan.txt"), headers.Select(header => header.Name)
                .Append("Matched source row: " + string.Join("|", visiblePlan)));
            FlaUI.Core.Capturing.Capture.Element(view).ToFile(Path.Combine(evidence, "live-plan.png"));
            Record("Passed", new { Trade = tradeId.Format(), SubscriptionCount = subscriptions.Count, TickContracts = ticks.Keys,
                StoredSourceEventId = storedSource.EventId,
                SourceEventId = observed.Id, observed.EventId, StoredRevision = stored!.PlanRevision,
                CompleteCalculation = stored.IronCondorTradePlanSnapshot!.IsComplete,
                Unavailable = stored.IronCondorTradePlanSnapshot.UnavailableReasons });
        }
        catch (Exception error)
        {
            Record("Failed", new { Stage = stage, Error = error.ToString(), Subscriptions = subscriptions.Keys, Ticks = ticks.Keys,
                Positions = positions.Count, Plans = plans.Count, Failures = failures.ToArray() });
            FlaUI.Core.Capturing.Capture.Screen().ToFile(Path.Combine(evidence, "failed-desktop.png"));
            if (view is not null) FlaUI.Core.Capturing.Capture.Element(view).ToFile(Path.Combine(evidence, "failed.png"));
            throw;
        }
        finally
        {
            try
            {
                if (view is not null) SelectCombo(view.FindFirstDescendant(cf => cf.ByAutomationId("ddlLiveFeed")), "LiveFeed OFF", automation);
                Record("FeedOffRequested", tradeId.Format());
                if (subscriptions.Count == 4)
                {
                    await WaitAsync(() => stopped.Count == 4, TimeSpan.FromSeconds(30));
                    Record("FourSubscriptionsReleased", stopped.Keys);
                }
            }
            finally
            {
                await listener.StopAsync(); await producer.StopAsync();
                File.WriteAllText(Path.Combine(evidence, "timeline.json"), JsonSerializer.Serialize(timeline.ToArray(), JsonOptions));
            }
        }
    }

    static void ClickModalButton(AutomationElement button)
    {
        Assert.True(button.IsEnabled);
        var handle = button.Properties.NativeWindowHandle.ValueOrDefault;
        if (handle != 0)
        {
            Assert.True(PostMessage(new IntPtr(handle), 0x00F5, IntPtr.Zero, IntPtr.Zero));
            return;
        }
        // ToolStrip items have no individual HWND. Post their mouse action to the containing native toolbar.
        var rectangle = button.BoundingRectangle;
        var parent = button.Parent;
        while (parent is not null && (handle = parent.Properties.NativeWindowHandle.ValueOrDefault) == 0) parent = parent.Parent;
        Assert.NotEqual(0, handle);
        var point = new NativePoint { X = (int)(rectangle.Left + rectangle.Width / 2), Y = (int)(rectangle.Top + rectangle.Height / 2) };
        Assert.True(ScreenToClient(new IntPtr(handle), ref point));
        var coordinates = new IntPtr(unchecked((point.Y << 16) | (point.X & 0xffff)));
        Assert.True(PostMessage(new IntPtr(handle), 0x0201, new IntPtr(1), coordinates));
        Assert.True(PostMessage(new IntPtr(handle), 0x0202, IntPtr.Zero, coordinates));
    }
    [StructLayout(LayoutKind.Sequential)]
    struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool ScreenToClient(IntPtr window, ref NativePoint point);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    // WinForms popup lists are separate HWNDs. Discover that window directly, then
    // use FlaUI's selection pattern; desktop traversal cannot locate these owned popups reliably.
    static void SelectCombo(FlaUI.Core.AutomationElements.AutomationElement element, string text, UIA3Automation automation)
    {
        Assert.True(element.IsEnabled);
        var combo = element.AsComboBox();
        combo.Expand();
        var info = new ComboBoxInfo { Size = Marshal.SizeOf<ComboBoxInfo>() };
        Assert.True(GetComboBoxInfo(new IntPtr(element.Properties.NativeWindowHandle.Value), ref info));
        var list = automation.FromHandle(info.List);
        var item = list.FindFirstDescendant(cf => cf.ByName(text))
            ?? throw new InvalidOperationException($"Combo item {text} was not exposed by its popup window.");
        item.Patterns.SelectionItem.Pattern.Select();
        combo.Collapse();
    }
    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    struct ComboBoxInfo { public int Size; public NativeRect ItemRect, ButtonRect; public int ButtonState; public IntPtr Combo, Item, List; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetComboBoxInfo(IntPtr handle, ref ComboBoxInfo info);

    // Integration evidence reads one committed full snapshot by its exact event-log ID.
    // This is test-only verification, never part of live monitoring or projection recovery.
    static async Task<IronCondorTradePlanUpdatedEvent> ReadSourceAsync(long eventId)
    {
        Assert.True(eventId > 0);
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "exec", Environment.GetEnvironmentVariable("IFM_LIVE_MONITORING_POSTGRES_CONTAINER") ?? "ifm_db",
            "psql", "-U", "postgres", "-d", "event-source-dev-db", "-Atc",
            $"SELECT encode(eventpayload,'hex') FROM event_log WHERE eventversion={eventId.ToString(CultureInfo.InvariantCulture)};" })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the source evidence query.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(); throw; }
        Assert.True(process.ExitCode == 0, await stderr);
        var payload = (await stdout).Trim();
        Assert.False(string.IsNullOrEmpty(payload), "The observed plan source event was not found in PostgreSQL.");
        return Assert.IsType<IronCondorTradePlanUpdatedEvent>(EventLogMessagePackCodec.Shared.Deserialize(
            typeof(IronCondorTradePlanUpdatedEvent).AssemblyQualifiedName!, eventId, Convert.FromHexString(payload)));
    }

    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    static string Required(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value : throw new InvalidOperationException($"Set {name} for this opt-in integration test.");
    static async Task<AutomationElement> WaitElementAsync(Func<AutomationElement?> read, TimeSpan timeout)
    {
        AutomationElement? result = null;
        await WaitAsync(() => (result = read()) is not null, timeout);
        return result!;
    }
    static Task WaitAsync(Func<bool> predicate, TimeSpan timeout, ConcurrentQueue<string>? failures = null)
        => WaitAsync(() => Task.FromResult(predicate()), timeout, failures);
    static async Task WaitAsync(Func<Task<bool>> predicate, TimeSpan timeout, ConcurrentQueue<string>? failures = null)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            if (failures?.TryPeek(out var failure) == true) throw new InvalidOperationException(failure);
            if (await predicate()) return;
            await Task.Delay(200);
        }
        throw new TimeoutException($"The live monitoring stage did not complete within {timeout}.");
    }
}
