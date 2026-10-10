using System.Runtime.InteropServices;
using DataGridViewRow = System.Windows.Forms.DataGridViewRow;
using DataGridView = System.Windows.Forms.DataGridView;
using ComboBox = System.Windows.Forms.ComboBox;
using FlaUI.Core.AutomationElements;
using System.Windows.Forms;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.Views.Trade;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class TradeBlotterLiveChainTests
{
    [Fact]
    [Trait("Category", "Live")]
    public async Task Live_four_leg_selection_latency_is_verified_with_FlaUI()
    {
        if (Environment.GetEnvironmentVariable("IFM_CHAIN_LATENCY_LIVE") != "true") return;
        var connection = new NatsConnectionManager();
        var producer = new NatsActorProducer(new NatsProducerOptions(), NullLogger.Instance, connection);
        await producer.StartAsync(new ActorMailboxId(ActorType.Query, "IFM.FlaUiChain." + Guid.NewGuid().ToString("N")), default);
        try
        {
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                using var context = new ApplicationContext();
                using var dispatcher = new Control();
                _ = dispatcher.Handle;
                dispatcher.BeginInvoke((Action)(async () =>
                {
                    try { await VerifyFourLegLiveAsync(new MarketDataQueryApi(producer)); completed.SetResult(); }
                    catch (Exception error) { completed.SetException(error); }
                    finally { context.ExitThread(); }
                }));
                System.Windows.Forms.Application.Run(context);
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(90));
        }
        finally { await producer.StopAsync(); await connection.DisposeAsync(); }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PopupRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    struct ComboPopupInfo { public int Size; public PopupRect ItemRect, ButtonRect; public int ButtonState; public IntPtr Combo, Item, List; }
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetComboBoxInfo(IntPtr handle, ref ComboPopupInfo info);

    static async Task VerifyFourLegLiveAsync(IMarketDataQueryApi api)
    {
        var expiry = DateOnly.Parse(Environment.GetEnvironmentVariable("IFM_CHAIN_EXPIRY") ?? "2026-11-20");
        var root = Substitute.For<IAppRoot>();
        var parameters = Substitute.For<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.IParameterSetsApi>();
        parameters.StartupRunsAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<ServiceResult<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.ParameterStartupRun[]>>(
            new ServiceOk<TomasAI.IFM.Domain.Reference.Shared.ParameterSets.ParameterStartupRun[]>([])));
        root.Services.ParameterSets.Returns(parameters);
        var feed = Substitute.For<IMarketDataFeedQueryApi>();
        feed.GetFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>()).Returns(Task.FromResult<ServiceResult<FuturesEodDataV2ReadModel>>(
            new ServiceFailed<FuturesEodDataV2ReadModel>(404, "No test-local EOD fixture")));
        feed.GetLastFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>()).Returns(Task.FromResult<ServiceResult<FuturesEodDataV2ReadModel>>(
            new ServiceFailed<FuturesEodDataV2ReadModel>(404, "No test-local EOD fixture")));
        root.Services.MarketDataQueries.Returns(new MarketDataQueryService(api, feed));
        var fund = new PortfolioFundEditorModel(0, "latency verification", "", 0, false, DateTime.UtcNow, "test");
        var order = new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel());
        var trade = new PortfolioFundOrderTradeEditorModel
        {
            TradeType = TradeType.ShortIronCondor, TradeState = TradeState.NewTrade,
            BaseContractId = "ES20261218", BaseContractSymbol = "ES", UnderlyingRoot = "ES",
            RequestedTradeDate = DateOnly.FromDateTime(DateTime.Today), RequestedMaturityDate = expiry
        };
        using var legacy = new FailingLegacyFeed();
        using var blotter = new EsTradeBlotterControl(root, fund, order, trade, 0, false, workflowControl: legacy);
        using var host = new Form { Width = 1400, Height = 900, Text = "IFM live option chain latency verification" };
        host.Controls.Add(blotter);
        host.Show();
        var selector = (ComboBox)blotter.Controls.Find("expirationSelector", true).Single();
        try { await WaitUntilAsync(() => selector.Items.Cast<object>().Any(x => x.ToString() == expiry.ToString("dd MMM yy", System.Globalization.CultureInfo.InvariantCulture)), TimeSpan.FromSeconds(30)); }
        catch (OperationCanceledException) { throw new InvalidOperationException("Expiry choices: " + string.Join(", ", selector.Items.Cast<object>()) + "; Status: " + blotter.Controls.Find("marketSelectionStatus", true).Single().Text); }
        var handle = host.Handle;
        await Task.Run(() =>
        {
            using var automation = new FlaUI.UIA3.UIA3Automation();
            var window = automation.FromHandle(handle);
            var combo = window.FindFirstDescendant(x => x.ByAutomationId("expirationSelector")).AsComboBox();
            combo.Expand();
            var info = new ComboPopupInfo { Size = Marshal.SizeOf<ComboPopupInfo>() };
            Assert.True(GetComboBoxInfo(new IntPtr(combo.Properties.NativeWindowHandle.Value), ref info));
            var popup = automation.FromHandle(info.List);
            var item = popup.FindFirstDescendant(x => x.ByName(expiry.ToString("dd MMM yy", System.Globalization.CultureInfo.InvariantCulture)));
            Assert.NotNull(item);
            item.Patterns.SelectionItem.Pattern.Select();
            combo.Collapse();
        });
        await WaitUntilAsync(() => selector.SelectedItem?.ToString() == expiry.ToString("dd MMM yy", System.Globalization.CultureInfo.InvariantCulture), TimeSpan.FromSeconds(5));
        Assert.Equal(expiry.ToString("dd MMM yy", System.Globalization.CultureInfo.InvariantCulture), selector.SelectedItem?.ToString());
        await WaitUntilAsync(() => ((FuturesOptionContractReadModel[])typeof(EsTradeBlotterControl).GetField("_availableOptionContracts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(blotter)!).Any(x => x.ContractId.StartsWith("ES" + expiry.ToString("yyyyMMdd"), StringComparison.Ordinal)), TimeSpan.FromSeconds(10));
        foreach (var (name, value) in new[] { ("shortCallDelta", 16m), ("callSpreadWidth", 50m), ("shortPutDelta", 16m), ("putSpreadWidth", 50m) })
            ((NumericUpDown)blotter.Controls.Find(name, true).Single()).Value = value;
        typeof(EsTradeBlotterControl).GetField("_spreadDefaultsLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(blotter, true);
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await blotter.SetLiveFeedAsync(true);
            var selected = (DataGridView)blotter.Controls.Find("selectedStrategyLegsGrid", true).Single();
            await WaitUntilAsync(() => selected.RowCount == 4 && (bool)typeof(EsTradeBlotterControl)
                .GetField("_chainReadyReported", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(blotter)!, TimeSpan.FromSeconds(30));
            var elapsed = started.Elapsed.TotalMilliseconds;
            await Task.Run(() =>
            {
                using var automation = new FlaUI.UIA3.UIA3Automation();
                var window = automation.FromHandle(handle);
                Assert.NotNull(window.FindFirstDescendant(x => x.ByAutomationId("selectedStrategyLegsGrid")));
                Assert.NotNull(window.FindFirstDescendant(x => x.ByAutomationId("marketSelectionStatus")));
            });
            var path = Environment.GetEnvironmentVariable("IFM_CHAIN_UI_REPORT") ?? Path.Combine(Path.GetTempPath(), "ifm-option-chain-ui-latency.json");
            await File.WriteAllTextAsync(path, System.Text.Json.JsonSerializer.Serialize(new { elapsed, expiry, SelectedLegs = selected.RowCount, FlaUiVerified = true }));
            Assert.True(elapsed <= 5000, $"UI four-leg readiness took {elapsed:F2}ms.");
        }
        finally { await blotter.SetLiveFeedAsync(false); }
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task Live_market_selection_renders_databento_iv_window_in_winforms()
    {
        if (Environment.GetEnvironmentVariable("IFM_UI_OCT1_LIVE_TEST") != "true") return;
        var connection = new NatsConnectionManager();
        var producer = new NatsActorProducer(new NatsProducerOptions(), NullLogger.Instance, connection);
        await producer.StartAsync(new ActorMailboxId(ActorType.Query,
            $"IFM.UiChain.{Guid.NewGuid():N}"), CancellationToken.None);
        try
        {
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                using var context = new ApplicationContext();
                using var dispatcher = new Control();
                _ = dispatcher.Handle;
                dispatcher.BeginInvoke((Action)(async () =>
                {
                    try { await VerifyLiveAsync(new MarketDataQueryApi(producer)); completed.SetResult(); }
                    catch (Exception error) { completed.SetException(error); }
                    finally { context.ExitThread(); }
                }));
                System.Windows.Forms.Application.Run(context);
            })
            { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(150));
        }
        finally
        {
            await producer.StopAsync();
            await connection.DisposeAsync();
        }
    }

    private static async Task VerifyLiveAsync(IMarketDataQueryApi api)
    {
        var expiry = new DateOnly(2026, 10, 1);
        var feed = Substitute.For<IMarketDataFeedQueryApi>();
        var noEod = Task.FromResult<ServiceResult<FuturesEodDataV2ReadModel>>(
            new ServiceFailed<FuturesEodDataV2ReadModel>(404, "No UI-local EOD fixture"));
        feed.GetFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>()).Returns(noEod);
        feed.GetLastFuturesEodDataAsync(Arg.Any<string>(), Arg.Any<DateOnly>()).Returns(noEod);
        var root = Substitute.For<IAppRoot>();
        root.Services.MarketDataQueries.Returns(new MarketDataQueryService(api, feed));
        var fund = new PortfolioFundEditorModel(0, "live test", "", 0, false, DateTime.UtcNow, "test");
        var order = new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel());
        var trade = new PortfolioFundOrderTradeEditorModel
        {
            TradeType = TradeType.ShortIronCondor,
            TradeState = TradeState.NewTrade,
            BaseContractId = "ES20261218",
            BaseContractSymbol = "ES",
            UnderlyingRoot = "ES",
            RequestedTradeDate = new DateOnly(2026, 9, 23),
            RequestedMaturityDate = expiry
        };
        using var legacy = new FailingLegacyFeed();
        using var blotter = new EsTradeBlotterControl(root, fund, order, trade, 0, false,
            workflowControl: legacy);
        using var host = new Form { Width = 1400, Height = 900, ShowInTaskbar = false, Opacity = 0 };
        host.Controls.Add(blotter);
        host.Show();
        var selector = (ComboBox)blotter.Controls.Find("expirationSelector", true).Single();
        await WaitUntilAsync(() => selector.Items.Cast<object>()
            .Any(item => item.ToString() == "01 Oct 26"), TimeSpan.FromSeconds(30));
        selector.SelectedItem = selector.Items.Cast<object>()
            .Single(item => item.ToString() == "01 Oct 26");
        await blotter.SetLiveFeedAsync(true);
        var liquidity = blotter.Controls.Find("liquiditySelector", true).Single();
        var grid = (DataGridView)blotter.Controls.Find("marketSelectionGrid", true).Single();
        await WaitUntilAsync(() => liquidity.Text == "ImpliedVolatility5Delta" && grid.RowCount > 0,
            TimeSpan.FromSeconds(100));
        Assert.Equal("ImpliedVolatility5Delta", liquidity.Text);
        Assert.True(grid.RowCount > 0);
        await WaitUntilAsync(() => Enumerable.Range(0, grid.RowCount).Any(index =>
            ReadVirtualCell(blotter, grid, "CallBid", index) is decimal
            || ReadVirtualCell(blotter, grid, "PutBid", index) is decimal),
            TimeSpan.FromSeconds(45));
        Assert.Contains(Enumerable.Range(0, grid.RowCount), index =>
            ReadVirtualCell(blotter, grid, "CallDelta", index) is string { Length: > 1 } call && call != "-"
            || ReadVirtualCell(blotter, grid, "PutDelta", index) is string { Length: > 1 } put && put != "-");
        Assert.Equal(0, legacy.LiveFeedCalls);
        await blotter.SetLiveFeedAsync(false);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        while (!predicate()) await Task.Delay(100, cancellation.Token);
    }

    private static object? ReadVirtualCell(EsTradeBlotterControl blotter, DataGridView grid,
        string column, int row)
    {
        var args = new DataGridViewCellValueEventArgs(grid.Columns[column]!.Index, row);
        typeof(EsTradeBlotterControl).GetMethod("MarketCellValueNeeded",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(blotter, [grid, args]);
        return args.Value;
    }

    [Fact]
    public async Task Market_selection_retains_values_across_partial_snapshots_until_updated_or_expiry_changes()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var context = new ApplicationContext();
            using var dispatcher = new Control();
            _ = dispatcher.Handle;
            dispatcher.BeginInvoke((Action)(() =>
            {
                try { VerifyPartialSnapshotRetention(); completed.SetResult(); }
                catch (Exception error) { completed.SetException(error); }
                finally { context.ExitThread(); }
            }));
            System.Windows.Forms.Application.Run(context);
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }

    private static void VerifyPartialSnapshotRetention()
    {
        var expiry = new DateOnly(2026, 10, 1);
        var observed = DateTimeOffset.UtcNow;
        var root = Substitute.For<IAppRoot>();
        root.Services.MarketDataQueries.Returns(new MarketDataQueryService(
            Substitute.For<IMarketDataQueryApi>(), Substitute.For<IMarketDataFeedQueryApi>()));
        var fund = new PortfolioFundEditorModel(0, "test", "", 0, false, DateTime.UtcNow, "test");
        var order = new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel());
        var trade = new PortfolioFundOrderTradeEditorModel
        {
            TradeType = TradeType.ShortIronCondor,
            TradeState = TradeState.NewTrade,
            BaseContractId = "ES20261218",
            BaseContractSymbol = "ES",
            UnderlyingRoot = "ES",
            RequestedTradeDate = new DateOnly(2026, 9, 23),
            RequestedMaturityDate = expiry
        };
        using var legacy = new FailingLegacyFeed();
        using var blotter = new EsTradeBlotterControl(root, fund, order, trade, 0, false,
            workflowControl: legacy);
        var grid = (DataGridView)blotter.Controls.Find("marketSelectionGrid", true).Single();
        var bind = typeof(EsTradeBlotterControl).GetMethod("BindEvaluatedChain",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        EvaluatedOptionContractReadModel Contract(string id, decimal strike, bool isCall,
            decimal? bid, double? delta) => new(id, strike, isCall, bid, null, null, null,
                null, null, null, null, delta, null, null, null, null, null, null,
                delta is not null, false, bid is null ? null : observed, null, observed);
        void Bind(DateOnly selectedExpiry, params EvaluatedOptionContractReadModel[] contracts) =>
            bind.Invoke(blotter, [new EvaluatedOptionChainReadModel("ES20261218", selectedExpiry,
                null, null, null, "ImpliedVolatility5Delta", observed, contracts)]);

        Bind(expiry, Contract("call-7800", 7800m, true, 10m, 0.5),
            Contract("put-7800", 7800m, false, 9m, -0.5));
        Bind(expiry, Contract("call-7800", 7800m, true, null, null),
            Contract("call-7850", 7850m, true, 0m, 0));
        Assert.Equal(2, grid.RowCount);
        Assert.Equal(0m, ReadVirtualCell(blotter, grid, "CallBid", 0));
        Assert.Equal(10m, ReadVirtualCell(blotter, grid, "CallBid", 1));
        Assert.Equal(9m, ReadVirtualCell(blotter, grid, "PutBid", 1));
        Assert.Equal("+0.500", ReadVirtualCell(blotter, grid, "CallDelta", 1));
        Bind(expiry, Contract("call-7800", 7800m, true, 11m, 0.55));
        Assert.Equal(11m, ReadVirtualCell(blotter, grid, "CallBid", 1));
        Assert.Equal(9m, ReadVirtualCell(blotter, grid, "PutBid", 1));
        Bind(new DateOnly(2026, 10, 2), Contract("call-7900", 7900m, true, 3m, 0.4));
        Assert.Equal(1, grid.RowCount);
        Assert.Equal(7900m, ReadVirtualCell(blotter, grid, "Strike", 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Market_selection_defaults_short_condor_to_16_delta_and_50_point_wings(bool missingWing)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var context = new ApplicationContext();
            using var dispatcher = new Control();
            _ = dispatcher.Handle;
            dispatcher.BeginInvoke((Action)(() =>
            {
                try { VerifyDefaultCondorSelection(missingWing); completed.SetResult(); }
                catch (Exception error) { completed.SetException(error); }
                finally { context.ExitThread(); }
            }));
            System.Windows.Forms.Application.Run(context);
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }

    private static void VerifyDefaultCondorSelection(bool missingWing)
    {
        var expiry = new DateOnly(2026, 10, 1);
        var observed = DateTimeOffset.UtcNow;
        var root = Substitute.For<IAppRoot>();
        root.Services.MarketDataQueries.Returns(new MarketDataQueryService(
            Substitute.For<IMarketDataQueryApi>(), Substitute.For<IMarketDataFeedQueryApi>()));
        var fund = new PortfolioFundEditorModel(0, "test", "", 0, false, DateTime.UtcNow, "test");
        var order = new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel());
        var trade = new PortfolioFundOrderTradeEditorModel
        {
            TradeType = TradeType.ShortIronCondor,
            TradeState = TradeState.NewTrade,
            BaseContractId = "ES20261218",
            BaseContractSymbol = "ES",
            UnderlyingRoot = "ES",
            RequestedTradeDate = new DateOnly(2026, 9, 23),
            RequestedMaturityDate = expiry
        };
        using var legacy = new FailingLegacyFeed();
        using var blotter = new EsTradeBlotterControl(root, fund, order, trade, 0, false,
            workflowControl: legacy);
        var grid = (DataGridView)blotter.Controls.Find("marketSelectionGrid", true).Single();
        var bind = typeof(EsTradeBlotterControl).GetMethod("BindEvaluatedChain",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        EvaluatedOptionContractReadModel Contract(string id, decimal strike, bool isCall, double delta) =>
            new(id, strike, isCall, 10m, 11m, 1, 1, null, null, 0.16, 10.5, delta,
                null, null, null, null, null, null, true, false, observed, null, observed);
        var contracts = new[]
        {
            Contract("call-7950", 7950m, true, 0.08),
            Contract("call-7900", 7900m, true, 0.161),
            Contract("call-7850", 7850m, true, 0.31),
            Contract("put-7750", 7750m, false, -0.30),
            Contract("put-7700", 7700m, false, -0.159),
            Contract("put-7650", 7650m, false, -0.08)
        };
        void Bind(EvaluatedOptionContractReadModel[] values) =>
            bind.Invoke(blotter, [new EvaluatedOptionChainReadModel("ES20261218", expiry,
                7800m, 7650m, 7950m, "ImpliedVolatility5Delta", observed, values)]);
        foreach (var (name, value) in new[] { ("shortCallDelta", 16m), ("callSpreadWidth", 50m),
                     ("shortPutDelta", 16m), ("putSpreadWidth", 50m) })
            ((NumericUpDown)blotter.Controls.Find(name, true).Single()).Value = value;
        typeof(EsTradeBlotterControl).GetField("_spreadDefaultsLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(blotter, true);
        if (missingWing)
        {
            var definition = new FuturesOptionContractReadModel("call-7950", "", "ES", "", "OPT", "USD", "CME", "50",
                new(2026, 10, 1), 7950, "C") { ExpirationUtc = new DateTimeOffset(2026, 10, 1, 20, 0, 0, TimeSpan.Zero) };
            typeof(EsTradeBlotterControl).GetField("_availableOptionContracts", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(blotter, new[] { definition });
        }
        Bind(missingWing ? contracts.Where(x => x.ContractId != "call-7950").ToArray() : contracts);
        Assert.Equal(16m, ((NumericUpDown)blotter.Controls.Find("shortCallDelta", true).Single()).Value);
        Assert.Equal(50m, ((NumericUpDown)blotter.Controls.Find("callSpreadWidth", true).Single()).Value);
        Assert.Equal(16m, ((NumericUpDown)blotter.Controls.Find("shortPutDelta", true).Single()).Value);
        Assert.Equal(50m, ((NumericUpDown)blotter.Controls.Find("putSpreadWidth", true).Single()).Value);
        Assert.Equal("+LC", ReadVirtualCell(blotter, grid, "CallSelected", 0));
        Assert.Equal("-SC", ReadVirtualCell(blotter, grid, "CallSelected", 1));
        Assert.Equal("-SP", ReadVirtualCell(blotter, grid, "PutSelected", 4));
        Assert.Equal("+LP", ReadVirtualCell(blotter, grid, "PutSelected", 5));
        Assert.Equal(
            ["CallSelected", "CallDelta", "CallOi", "CallVolume", "CallBid", "CallAsk",
                "Strike", "PutBid", "PutAsk", "PutVolume", "PutOi", "PutDelta", "PutSelected"],
            grid.Columns.Cast<DataGridViewColumn>().Select(column => column.Name));
        var selected = (DataGridView)blotter.Controls.Find("selectedStrategyLegsGrid", true).Single();
        Assert.Equal(4, selected.RowCount);
        var selectedLayout = (TableLayoutPanel)blotter.Controls.Find("marketSelectionLayout", true).Single();
        Assert.True(selectedLayout.RowStyles[4].Height >=
            selected.Rows.Cast<DataGridViewRow>().Sum(row => row.Height) + 16);
        object? SelectedValue(string column, int row)
        {
            var args = new DataGridViewCellValueEventArgs(selected.Columns[column]!.Index, row);
            typeof(EsTradeBlotterControl).GetMethod("SelectedLegCellValueNeeded",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(blotter, [selected, args]);
            return args.Value;
        }
        Assert.Equal("+LC", SelectedValue("CallSelected", 0));
        if (missingWing) Assert.Null(SelectedValue("CallBid", 0));
        Assert.Equal("-SC", SelectedValue("CallSelected", 1));
        Assert.Equal("-SP", SelectedValue("PutSelected", 2));
        Assert.Equal("+LP", SelectedValue("PutSelected", 3));
        Assert.Equal(10m, SelectedValue("PutBid", 2));
        Assert.Null(SelectedValue("CallBid", 2));
        var blotterTabs = (TabControl)blotter.Controls.Find("tradeBlotterTabs", true).Single();
        Assert.Equal(3, blotterTabs.TabPages.Count);
        Assert.Equal("Market Selection", blotterTabs.TabPages[0].Text);
        Assert.Equal("Broker Trade", blotterTabs.TabPages[1].Text);
        Assert.Equal("Order Fills", blotterTabs.TabPages[2].Text);

        var format = typeof(EsTradeBlotterControl).GetMethod("MarketGridCellFormatting",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Color BackColor(string column, int row)
        {
            var style = new DataGridViewCellStyle();
            var args = new DataGridViewCellFormattingEventArgs(
                grid.Columns[column]!.Index, row, null, typeof(string), style);
            format.Invoke(blotter, [grid, args]);
            return args.CellStyle.BackColor;
        }
        Assert.Equal(Color.FromArgb(110, 24, 30), BackColor("CallSelected", 1));
        Assert.Equal(Color.FromArgb(20, 54, 105), BackColor("CallSelected", 0));
        Assert.Equal(Color.FromArgb(110, 24, 30), BackColor("PutSelected", 4));
        Assert.Equal(Color.FromArgb(20, 54, 105), BackColor("PutSelected", 5));
        Assert.All(grid.Columns.Cast<DataGridViewColumn>(),
            column => Assert.Equal(Color.Yellow, BackColor(column.Name, 3)));
        var selectedFormat = typeof(EsTradeBlotterControl).GetMethod("SelectedLegCellFormatting",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var selectedStyle = new DataGridViewCellStyle();
        selectedFormat.Invoke(blotter, [selected, new DataGridViewCellFormattingEventArgs(
            selected.Columns["PutSelected"]!.Index, 2, null, typeof(string), selectedStyle)]);
        Assert.Equal(Color.FromArgb(110, 24, 30), selectedStyle.BackColor);

        Bind(contracts.Select(value => value with { Delta = null }).ToArray());
        Assert.Equal("-SC", ReadVirtualCell(blotter, grid, "CallSelected", 1));
        Assert.Equal("+LP", ReadVirtualCell(blotter, grid, "PutSelected", 5));
        Assert.Equal(4, selected.RowCount);
    }

    [Fact]
    public async Task Market_selection_defaults_call_credit_vertical_to_16_delta_and_50_point_width()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var context = new ApplicationContext();
            using var dispatcher = new Control();
            _ = dispatcher.Handle;
            dispatcher.BeginInvoke((Action)(() =>
            {
                try { VerifyDefaultVerticalSelection(); completed.SetResult(); }
                catch (Exception error) { completed.SetException(error); }
                finally { context.ExitThread(); }
            }));
            System.Windows.Forms.Application.Run(context);
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }

    private static void VerifyDefaultVerticalSelection()
    {
        var expiry = new DateOnly(2026, 10, 1);
        var observed = DateTimeOffset.UtcNow;
        var root = Substitute.For<IAppRoot>();
        root.Services.MarketDataQueries.Returns(new MarketDataQueryService(
            Substitute.For<IMarketDataQueryApi>(), Substitute.For<IMarketDataFeedQueryApi>()));
        var fund = new PortfolioFundEditorModel(0, "test", "", 0, false, DateTime.UtcNow, "test");
        var order = new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel());
        var trade = new PortfolioFundOrderTradeEditorModel
        {
            TradeType = TradeType.CallCreditSpread,
            TradeState = TradeState.NewTrade,
            BaseContractId = "ES20261218",
            BaseContractSymbol = "ES",
            UnderlyingRoot = "ES",
            RequestedTradeDate = new DateOnly(2026, 9, 23),
            RequestedMaturityDate = expiry
        };
        using var legacy = new FailingLegacyFeed();
        using var blotter = new EsTradeBlotterControl(root, fund, order, trade, 0, false,
            workflowControl: legacy);
        ((NumericUpDown)blotter.Controls.Find("shortLegDelta", true).Single()).Value = 16m;
        ((NumericUpDown)blotter.Controls.Find("spreadWidth", true).Single()).Value = 50m;
        typeof(EsTradeBlotterControl).GetField("_spreadDefaultsLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(blotter, true);
        EvaluatedOptionContractReadModel Contract(string id, decimal strike, double delta) =>
            new(id, strike, true, 10m, 11m, 1, 1, null, null, 0.16, 10.5, delta,
                null, null, null, null, null, null, true, false, observed, null, observed);
        var chain = new EvaluatedOptionChainReadModel("ES20261218", expiry, 7800m, 7800m, 8000m,
            "ImpliedVolatility5Delta", observed,
            [Contract("call-7900", 7900m, .161), Contract("call-7950", 7950m, .08)]);
        typeof(EsTradeBlotterControl).GetMethod("BindEvaluatedChain",
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(blotter, [chain]);

        var selected = (DataGridView)blotter.Controls.Find("selectedStrategyLegsGrid", true).Single();
        Assert.Equal(2, selected.RowCount);
        object? SelectedValue(string column, int row)
        {
            var args = new DataGridViewCellValueEventArgs(selected.Columns[column]!.Index, row);
            typeof(EsTradeBlotterControl).GetMethod("SelectedLegCellValueNeeded",
                BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(blotter, [selected, args]);
            return args.Value;
        }
        Assert.Equal("+LC", SelectedValue("CallSelected", 0));
        Assert.Equal(7950m, SelectedValue("Strike", 0));
        Assert.Equal("-SC", SelectedValue("CallSelected", 1));
        Assert.Equal(7900m, SelectedValue("Strike", 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Market_selection_polls_iv_window_without_starting_legacy_leg_feed(bool initialLiveFeed)
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var context = new ApplicationContext();
            using var dispatcher = new Control();
            _ = dispatcher.Handle;
            dispatcher.BeginInvoke((Action)(async () =>
            {
                try { await VerifyAsync(initialLiveFeed); completed.SetResult(); }
                catch (Exception error) { completed.SetException(error); }
                finally { context.ExitThread(); }
            }));
            System.Windows.Forms.Application.Run(context);
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }

    private static async Task VerifyAsync(bool initialLiveFeed)
    {
        var expiry = new DateOnly(2026, 10, 1);
        var observed = DateTimeOffset.UtcNow;
        var chain = new EvaluatedOptionChainReadModel("ES20261218", expiry, 7800m, 7500m, 7900m,
            "ImpliedVolatility5Delta", observed,
            [new EvaluatedOptionContractReadModel("call-7800", 7800m, true, 10m, 10.5m, 1, 2,
                null, null, 0.16, 10.25, 0.5, 0.01, 0.1, -0.2, 0.01,
                null, null, true, false, observed, null, observed)]);
        var api = Substitute.For<IMarketDataQueryApi>();
        var reads = 0;
        var requests = new List<GetEvaluatedOptionChainQuery>();
        api.GetEvaluatedOptionChainAsync(Arg.Any<GetEvaluatedOptionChainQuery>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<GetEvaluatedOptionChainQuery>();
                if (!query.ReleaseOnly)
                {
                    requests.Add(query);
                    Interlocked.Increment(ref reads);
                }
                return Task.FromResult<ServiceResult<EvaluatedOptionChainReadModel>>(
                    new ServiceOk<EvaluatedOptionChainReadModel>(chain));
            });
        var root = Substitute.For<IAppRoot>();
        root.Services.MarketDataQueries.Returns(new MarketDataQueryService(api,
            Substitute.For<IMarketDataFeedQueryApi>()));
        var fund = new PortfolioFundEditorModel(0, "test", "", 0, false, DateTime.UtcNow, "test");
        var order = new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel());
        var trade = new PortfolioFundOrderTradeEditorModel
        {
            TradeType = TradeType.ShortIronCondor,
            TradeState = TradeState.NewTrade,
            BaseContractId = "ES20261218",
            BaseContractSymbol = "ES",
            UnderlyingRoot = "ES",
            RequestedTradeDate = new DateOnly(2026, 9, 23),
            RequestedMaturityDate = expiry
        };
        using var legacy = new FailingLegacyFeed();
        using var blotter = new EsTradeBlotterControl(root, fund, order, trade, 0, false,
            capabilities: TomasAI.IFM.Application.TradeBroker.Contracts.BrokerCapabilities.Emulator("TEST-EMULATOR"),
            workflowControl: legacy, liveFeedEnabled: initialLiveFeed);
        blotter.BindAvailableExpiries([expiry], trade.RequestedTradeDate, expiry);

        await blotter.SetLiveFeedAsync(true);
        Assert.Equal(1, Volatile.Read(ref reads));
        Assert.False(requests[^1].AllowFrozenEmulatorPreview);
        Assert.False(requests[^1].FrozenEmulatorPreviewOnly);
        var grid = (DataGridView)blotter.Controls.Find("marketSelectionGrid", true).Single();
        Assert.Equal(1, grid.RowCount);
        Assert.Equal("ImpliedVolatility5Delta",
            blotter.Controls.Find("liquiditySelector", true).Single().Text);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        while (Volatile.Read(ref reads) < 2) await Task.Delay(50, timeout.Token);
        await blotter.SetLiveFeedAsync(false);
        Assert.True(requests[^1].AllowFrozenEmulatorPreview);
        Assert.True(requests[^1].FrozenEmulatorPreviewOnly);
        Assert.Equal(0, legacy.LiveFeedCalls);
    }

    private sealed class FailingLegacyFeed : Control, ITradeOrderControl
    {
        public int LiveFeedCalls { get; private set; }
        public DateOnly MaturityDate => new(2026, 10, 1);
        public Task RemoveTradeAsync(int fundid, int orderId, int tradeId) => Task.CompletedTask;
        public Task<Guid> SubmitOrderAsync(DateOnly tradeDate, OrderActionType orderAction,
            ITradeOrderConfirmationService tradeOrderConfirmation) => Task.FromResult(Guid.Empty);
        public Task SetLiveFeedAsync(bool enabled)
        {
            LiveFeedCalls++;
            throw new InvalidOperationException("Legacy feed must not gate Market Selection.");
        }
        public void SetNearestStrikePrices() { }
        public Task OrderActionTypeChangedAsync(OrderActionType orderActionType) => Task.CompletedTask;
    }
}
