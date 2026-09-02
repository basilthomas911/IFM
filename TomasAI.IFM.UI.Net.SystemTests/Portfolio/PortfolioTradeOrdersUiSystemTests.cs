using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.Views.Trade;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Views.Trade.IronCondor;
using TomasAI.IFM.UI.Net.Views.Portfolio;
using TomasAI.IFM.UI.Net.Services.Application;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class PortfolioTradeOrdersUiSystemTests
{
    [Fact]
    [Trait("Category", "PortfolioTypography")]
    public void Portfolio_administration_uses_Microsoft_Sans_Serif_ten_point_throughout()
    {
        using var form = new PortfolioAdministrationForm();

        ControlsAndSelf(form).Should().OnlyContain(control =>
            control.Font.Name == "Microsoft Sans Serif"
            && Math.Abs(control.Font.Size - 10F) < 0.01F);
        ControlsAndSelf(form).OfType<DataGridView>().Should().OnlyContain(grid =>
            grid.ColumnHeadersDefaultCellStyle.Font.Name == "Microsoft Sans Serif"
            && Math.Abs(grid.ColumnHeadersDefaultCellStyle.Font.Size - 10F) < 0.01F);

        Field<Label>(form, "_menuTitle").Font.Style.Should().HaveFlag(FontStyle.Bold);
    }

    [Fact]
    [Trait("Category", "TradeOrdersTypography")]
    public void Trade_Orders_uses_one_font_family_and_point_size_for_existing_and_embedded_controls()
    {
        using var form = new TradeOrderEditorForm(Substitute.For<IAppRoot>(), Substitute.For<IReferenceDataService>());

        var tradeControlPanel = Field<Panel>(form, "pnlTradeControl");
        using var embeddedPanel = new Panel();
        using var embeddedValue = new Label
        {
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            Text = "Embedded value",
        };
        embeddedPanel.Controls.Add(embeddedValue);
        tradeControlPanel.Controls.Add(embeddedPanel);

        ControlsAndSelf(form).Should().OnlyContain(control =>
            control.Font.Name == "Microsoft Sans Serif"
            && Math.Abs(control.Font.Size - 10F) < 0.01F);
        embeddedValue.Font.Style.Should().HaveFlag(FontStyle.Bold);
        ControlsAndSelf(form).OfType<DateTimePicker>().Should().OnlyContain(picker =>
            picker.CalendarFont.Name == "Microsoft Sans Serif"
            && Math.Abs(picker.CalendarFont.Size - 10F) < 0.01F);
    }

    [Fact]
    [Trait("Gate", "PF-28")]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public void Trade_Orders_scopes_Portfolio_before_Fund_and_removes_Create_Fund()
    {
        using var form = new TradeOrderEditorForm(Substitute.For<IAppRoot>(), Substitute.For<IReferenceDataService>());

        var portfolio = Field<ComboBox>(form, "_portfolioSelector");
        var fund = Field<ComboBox>(form, "ddlFund");
        var source = Field<ComboBox>(form, "_sourceFilter");
        var createFund = Field<Button>(form, "btnCreateFund");
        var mode = Field<ComboBox>(form, "_historyModeSelector");
        var openLegacy = Field<Button>(form, "btnOpenTrade");
        var tradesPanel = Field<Panel>(form, "pnlTrades");
        var orders = Field<ListView>(form, "lstTradeOrders");
        var trades = Field<ListView>(form, "lstTrades");
        var tradeControl = Field<Panel>(form, "pnlTradeControl");
        var fromCalendar = Field<DateTimePicker>(form, "dtpFrom");
        var orderLabel = Field<Label>(form, "lblTradeOrders");
        var tradesLabel = Field<Label>(form, "label1");
        var tradeTypeLabel = Field<Label>(form, "lblTradeType");
        var daysToExpiry = Field<TextBox>(form, "txtDaysToExpiry");
        var orderActionLabel = Field<Label>(form, "lblOrderAction");
        var orderAction = Field<ComboBox>(form, "ddlOrderActionType");
        var liveFeed = Field<CheckBox>(form, "cbLiveFeed");
        var loadOrder = Field<Button>(form, "btnLoadOrder");
        var completeOrder = Field<Button>(form, "btnCompleteOrder");
        var submitOrder = Field<Button>(form, "btnSubmitOrder");
        var endOfDay = Field<Button>(form, "btnEndOfDay");
        var targetStateLabel = Field<Label>(form, "lblTradeStateTarget");
        var targetState = Field<ComboBox>(form, "ddlTradeState");
        var tradePositionPanel = Field<Panel>(form, "pnlTradePosition");
        var portfolioLabel = ControlsAndSelf(form).OfType<Label>().Single(label => label.Text == "Portfolio:");
        var fundLabel = Field<Label>(form, "lblFundSelector");
        var fromLabel = Field<Label>(form, "lblFrom");

        portfolio.AccessibleName.Should().Be("Portfolio selector");
        portfolio.Top.Should().BeLessThan(fund.Top);
        portfolioLabel.Left.Should().Be(fromLabel.Left);
        fundLabel.Left.Should().Be(fromLabel.Left);
        orderLabel.Left.Should().Be(fromLabel.Left);
        tradesLabel.Left.Should().Be(fromLabel.Left);
        tradeTypeLabel.Left.Should().BeLessThan(fromLabel.Left);
        fromCalendar.Left.Should().Be(orders.Left);
        orderActionLabel.Left.Should().Be(daysToExpiry.Right + 16);
        liveFeed.Left.Should().Be(orderAction.Right + 16);
        liveFeed.Top.Should().Be(orderAction.Top + 1);
        source.Items.Cast<string>().Should().Equal("All", "Manual", "Strategy Workflow");
        mode.Items.Cast<string>().Should().Equal("Current", "Legacy History");
        mode.AccessibleName.Should().Be("Trade history mode");
        form.ClientSize.Height.Should().Be(1080);
        form.ClientSize.Width.Should().Be(1440);
        form.FormBorderStyle.Should().Be(FormBorderStyle.Sizable);
        openLegacy.Parent.Should().BeSameAs(tradesPanel);
        openLegacy.Text.Should().Be("View Legacy Trade...");
        trades.Width.Should().Be(orders.Width);
        tradeControl.Width.Should().Be(orders.Width);
        tradeControl.Height.Should().Be(280);
        tradeControl.Bottom.Should().BeLessThanOrEqualTo(
            Field<Panel>(form, "pnlTradePosition").ClientSize.Height);
        fund.Width.Should().Be(orders.Width);
        loadOrder.Top.Should().Be(orders.Top);
        completeOrder.Bottom.Should().Be(orders.Bottom);
        submitOrder.Top.Should().Be(tradeControl.Top);
        endOfDay.Top.Should().Be(submitOrder.Bottom + 8);
        targetStateLabel.Parent.Should().BeSameAs(tradePositionPanel);
        targetState.Parent.Should().BeSameAs(tradePositionPanel);
        targetStateLabel.Left.Should().Be(endOfDay.Left);
        targetStateLabel.Top.Should().Be(endOfDay.Bottom + 8);
        targetStateLabel.Width.Should().Be(endOfDay.Width);
        targetState.Left.Should().Be(endOfDay.Left);
        targetState.Top.Should().Be(targetStateLabel.Bottom + 4);
        targetState.Width.Should().Be(endOfDay.Width);
        ControlsAndSelf(form).OfType<Button>().Should().OnlyContain(button =>
            button.Width == 140 && button.Height == 32);
        form.ClientSize = new Size(1300, 800);
        form.PerformLayout();
        trades.Width.Should().Be(orders.Width);
        tradeControl.Width.Should().Be(orders.Width);
        fund.Width.Should().Be(orders.Width);
        createFund.Visible.Should().BeFalse();
        createFund.Enabled.Should().BeFalse();
        Field<ListView>(form, "lstTradeOrders").Columns.Cast<ColumnHeader>().Select(x => x.Text).Should().Contain("Source");
    }

    static IEnumerable<Control> ControlsAndSelf(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
        foreach (var descendant in ControlsAndSelf(child))
            yield return descendant;
    }

    static int BottomWithin(Control control, Control root)
    {
        var bottom = control.Bottom;
        for (var parent = control.Parent; parent is not null && parent != root; parent = parent.Parent)
            bottom += parent.Top;
        return bottom;
    }

    static int TopWithin(Control control, Control root)
    {
        var top = control.Top;
        for (var parent = control.Parent; parent is not null && parent != root; parent = parent.Parent)
            top += parent.Top;
        return top;
    }

    static bool FitsWithinTableCell(TableLayoutPanel table, Control control)
    {
        var row = table.GetRow(control);
        var rowSpan = table.GetRowSpan(control);
        var rowHeights = table.GetRowHeights();
        var cellTop = rowHeights.Take(row).Sum();
        var cellBottom = cellTop + rowHeights.Skip(row).Take(rowSpan).Sum();

        return control.Top >= cellTop && control.Bottom <= cellBottom;
    }

    [Fact]
    [Trait("Category", "TradeOrdersLayout")]
    public void Loaded_trade_blotter_measures_scaled_content_and_grows_the_dialog_to_keep_it_visible()
    {
        using var form = new TradeOrderEditorForm(
            Substitute.For<IAppRoot>(),
            Substitute.For<IReferenceDataService>());
        var originalClientHeight = form.ClientSize.Height;
        var host = Field<Panel>(form, "pnlTradeControl");
        var outer = Field<Panel>(form, "pnlTradePosition");
        using var blotter = new Panel
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(0, 291),
        };
        using var scaledBottomRow = new Panel
        {
            Top = 470,
            Height = 30,
            Visible = true,
        };
        blotter.Controls.Add(scaledBottomRow);

        host.Controls.Add(blotter);
        form.PerformLayout();

        host.Height.Should().BeGreaterThanOrEqualTo(scaledBottomRow.Bottom + 8);
        (outer.ClientSize.Height - host.Bottom).Should().BeGreaterThanOrEqualTo(12);
        form.ClientSize.Height.Should().BeGreaterThan(originalClientHeight);
    }

    [Fact]
    [Trait("Gate", "PF-31")]
    [Trait("Category", "PortfolioLegacyHistory")]
    public void Legacy_trade_opens_or_activates_one_read_only_main_screen_tab()
    {
        using var host = new TabControl();
        var composition = new FundOrderTradeReadModel
        {
            FundId = 1004, OrderId = 1084, TradeId = 1090, TradeType = TradeType.ShortIronCondor,
            TradeState = TradeState.OrderFilled,
        };
        var history = new LegacyFundTradeHistoryReadModel
        {
            Composition = composition,
            MatchStatus = LegacyTradeMatchStatus.PositionHistory,
        };

        var first = LegacyTradeHistoryTabFactory.OpenOrActivate(host, history);
        var second = LegacyTradeHistoryTabFactory.OpenOrActivate(host, history);

        second.Should().BeSameAs(first);
        host.TabPages.Cast<TabPage>().Should().ContainSingle();
        host.SelectedTab.Should().BeSameAs(first);
        first.Text.Should().Be("1084:1090");
        first.Controls.Cast<Control>().Should().ContainSingle(x => x is LegacyTradeHistoryView);
        ((LegacyTradeHistoryView)first.Controls[0]).IsReadOnly.Should().BeTrue();
    }

    [Fact]
    [Trait("Gate", "PF-31")]
    [Trait("Category", "PortfolioLegacyHistory")]
    public void TradeDb_backed_legacy_trade_uses_actual_historical_blotter_and_reuses_its_tab()
    {
        using var host = new TabControl();
        var composition = new FundOrderTradeReadModel
        {
            FundId = 1004, OrderId = 1084, TradeId = 1090, TradeType = TradeType.ShortIronCondor,
            TradeState = TradeState.OrderFilled,
        };
        var tradeDbTrade = new OptionTradeReadModel
        {
            OrderId = 1084, TradeId = 1090, TradeType = TradeType.ShortIronCondor,
            TradeDate = new DateOnly(2024, 1, 2),
        };
        var history = new LegacyFundTradeHistoryReadModel
        {
            Composition = composition,
            TradeDbTrade = tradeDbTrade,
            MatchStatus = LegacyTradeMatchStatus.PositionHistory,
        };
        var actualViewer = new HistoricalTradeViewerStub();

        var first = LegacyTradeHistoryTabFactory.OpenOrActivate(host, history, page =>
        {
            host.TabPages.Contains(page).Should().BeTrue("the viewer requires a measured, hosted TabPage");
            return actualViewer;
        });
        var second = LegacyTradeHistoryTabFactory.OpenOrActivate(host, history, _ => throw new InvalidOperationException("duplicate viewer"));

        second.Should().BeSameAs(first);
        first.Controls.Cast<Control>().Should().ContainSingle().Which.Should().BeSameAs(actualViewer);
        first.Controls.Cast<Control>().Should().NotContain(x => x is LegacyTradeHistoryView);
        first.Tag.Should().BeSameAs(actualViewer);
        actualViewer.OpenCount.Should().Be(1);
    }

    [Fact]
    [Trait("Gate", "PF-31")]
    [Trait("Category", "PortfolioLegacyHistory")]
    public void Historical_iron_condor_factory_creates_the_actual_graph_blotter_with_live_feed_disabled()
    {
        var root = Substitute.For<IAppRoot>();
        var services = Substitute.For<IUiServiceCatalog>();
        var commandResponses = new CommandResponseEventService(Substitute.For<ICommandResponseUIEventConsumer>());
        commandResponses.SetSiteId(Guid.NewGuid());
        root.Services.Returns(services);
        services.CommandResponses.Returns(commandResponses);
        var fund = new FundReadModel(1004, "Legacy Fund", "history", 0m, false, DateTime.UtcNow, "legacy");
        var order = new FundOrderReadModel(1004, 1084, DateTime.UtcNow, TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Open, "ES",
            new DateOnly(2024, 1, 2), new DateOnly(2024, 2, 2), "history", DateTime.UtcNow, "legacy", null, string.Empty);
        var composition = new FundOrderTradeReadModel
        {
            FundId = 1004, OrderId = 1084, TradeId = 1090, TradeType = TradeType.ShortIronCondor,
            BaseContractSymbol = "ES", TradeDate = new DateOnly(2024, 1, 2),
        };
        using var host = new Panel { Size = Size.Empty };

        using var viewer = TradeBlotterFactory.Create(host, root, fund, order, composition,
            new DateOnly(2024, 1, 31), [], historicalReadOnly: true);

        viewer.Should().BeOfType<IronCondorView>();
        var ironCondor = (IronCondorView)viewer!;
        ironCondor.IsHistoricalReadOnly.Should().BeTrue();
        ironCondor.Dock.Should().Be(DockStyle.Fill);
        Field<ComboBox>(ironCondor, "ddlLiveFeed").Enabled.Should().BeFalse();
        var charts = new[]
        {
            Field<System.Windows.Forms.DataVisualization.Charting.Chart>(ironCondor, "graphEodData"),
            Field<System.Windows.Forms.DataVisualization.Charting.Chart>(ironCondor, "graphSpreadDistribution"),
        };
        ControlsAndSelf(ironCondor).Should().OnlyContain(control =>
            control.Font.Name == "Microsoft Sans Serif"
            && Math.Abs(control.Font.Size - 10F) < 0.01F);
        charts.Should().OnlyContain(chart => ChartUsesTradeTypography(chart));
        charts.Should().OnlyContain(chart => chart.Titles.Count == 0 && chart.Dock == DockStyle.Fill);
        var graphTabs = Field<TabControl>(ironCondor, "_graphTabs");
        graphTabs.GetType().Name.Should().Be("DarkTabControl");
        graphTabs.Dock.Should().Be(DockStyle.Fill);
        graphTabs.TabPages.Cast<TabPage>().Select(page => page.Text).Should().Equal(
            "Iron Condor Net Spread Path",
            "Futures Bollinger Bands");
        graphTabs.SelectedTab.Should().BeSameAs(graphTabs.TabPages[0]);
        graphTabs.SelectedTab!.Controls.Cast<Control>().Should().ContainSingle()
            .Which.Should().BeSameAs(charts[1]);

        host.Controls.Add(ironCondor);
        host.Size = new Size(1800, 900);
        ((IFormControl)ironCondor).Resize(host);
        var realTimeData = Field<SplitContainer>(ironCondor, "pnlRealTimeData");
        var history = Field<Panel>(ironCondor, "pnlIronCondorTrade");
        var historySplitter = Field<SplitContainer>(ironCondor, "pnlTradeSplitter");
        var historyList = Field<ListView>(ironCondor, "lstTradeHistory");
        var contractIds = Field<ListView>(ironCondor, "lstOptionContractIds");
        var contractDetails = Field<Panel>(ironCondor, "pnlTradeHistory");
        var topLayout = Field<TableLayoutPanel>(ironCondor, "_primaryTopLayout");
        var realTimeHeader = Field<Panel>(ironCondor, "pnlRealTimeHeaderData");
        var realTimeGrid = Field<DataGridView>(ironCondor, "gridRealTimeOptionData");
        var spreadRows = Field<TableLayoutPanel>(ironCondor, "pnlIronCondorTradeDataRt");
        var realTimeSummary = Field<TableLayoutPanel>(ironCondor, "tableLayoutPanel1");
        var realTimeStatus = Field<TableLayoutPanel>(ironCondor, "pnlRt");
        var assetSplitter = Field<SplitContainer>(ironCondor, "pnlAssetSplitter");
        var logTabs = Field<TabControl>(ironCondor, "tabActionData");
        logTabs.GetType().Name.Should().Be("DarkTabControl");
        new[] { assetSplitter, realTimeData, historySplitter }
            .Should().OnlyContain(splitter => splitter.BackColor.ToArgb() == Color.Black.ToArgb());
        graphTabs.TabPages.Cast<TabPage>()
            .Concat(logTabs.TabPages.Cast<TabPage>())
            .Should().OnlyContain(page => page.BackColor.ToArgb() == Color.Black.ToArgb());
        realTimeData.Visible.Should().BeTrue();
        realTimeData.Dock.Should().Be(DockStyle.Fill);
        graphTabs.SelectedTab.Should().BeSameAs(graphTabs.TabPages[0]);
        graphTabs.Width.Should().Be((ironCondor.Width - 640 - 10) / 2);
        history.Height.Should().Be(graphTabs.Height);
        history.Right.Should().Be(graphTabs.Left);
        topLayout.Width.Should().Be(realTimeData.Panel1.ClientSize.Width);
        realTimeHeader.Width.Should().Be(realTimeData.Panel2.ClientSize.Width);
        realTimeGrid.Width.Should().Be(realTimeData.Panel2.ClientSize.Width);
        realTimeData.Panel2.BackColor.Should().Be(Color.Black);
        realTimeGrid.BackgroundColor.Should().Be(Color.Black);
        realTimeData.Panel2.ClientSize.Height.Should().Be(169);
        realTimeHeader.Bottom.Should().BeLessThanOrEqualTo(realTimeData.Panel2.ClientSize.Height);
        (realTimeData.Panel2.ClientSize.Height - realTimeHeader.Bottom).Should().BeLessThanOrEqualTo(10);
        realTimeStatus.Top.Should().Be(realTimeSummary.Bottom);
        spreadRows.Top.Should().Be(realTimeStatus.Bottom + 3);
        spreadRows.RowStyles.Cast<RowStyle>().Select(row => row.Height).Should().Equal(36F, 25F, 25F);
        (realTimeHeader.ClientSize.Height - spreadRows.Bottom).Should().Be(2);
        spreadRows.Bottom.Should().BeLessThanOrEqualTo(realTimeHeader.ClientSize.Height);
        new[]
            {
                Field<TextBox>(ironCondor, "txtPutSpreadType"),
                Field<TextBox>(ironCondor, "txtCallSpreadType"),
            }
            .Should().OnlyContain(control =>
                control.Visible && control.Bottom <= spreadRows.ClientSize.Height);
        realTimeGrid.ClientSize.Height.Should().BeGreaterThan(0);
        new[]
            {
                realTimeSummary,
                realTimeStatus,
                spreadRows,
            }
            .Should().OnlyContain(table =>
                table.Right >= realTimeHeader.ClientSize.Width - 5
                && table.ColumnStyles.Cast<ColumnStyle>().All(column => column.SizeType == SizeType.Percent));
        charts[1].ClientSize.Width.Should().BeGreaterThan(0);
        charts[1].ClientSize.Height.Should().BeGreaterThan(0);
        logTabs.Height.Should().BeInRange(
            (assetSplitter.ClientSize.Height - assetSplitter.SplitterWidth) / 3 - 1,
            (assetSplitter.ClientSize.Height - assetSplitter.SplitterWidth) / 3 + 1);
        contractIds.Items.AddRange(Enumerable.Range(1, 5)
            .Select(index => new ListViewItem($"Contract {index}"))
            .ToArray());
        contractIds.CreateControl();
        Invoke(ironCondor, "FitContractIdPaneToFourRows");
        var fixedContractHeight = contractIds.GetItemRect(0).Top
            + contractIds.GetItemRect(0).Height * 4
            + 3;
        historySplitter.Panel2.ClientSize.Height.Should().Be(fixedContractHeight + 79);
        contractDetails.ClientSize.Height.Should().Be(fixedContractHeight);
        contractIds.GetItemRect(3).Bottom.Should().BeLessThanOrEqualTo(contractIds.ClientSize.Height);
        contractIds.GetItemRect(4).Bottom.Should().BeGreaterThan(contractIds.ClientSize.Height);

        var initialHistoryListHeight = historyList.Height;
        var initialGraphHeight = graphTabs.Height;
        host.Size = new Size(1800, 1200);
        ((IFormControl)ironCondor).Resize(host);
        historySplitter.Panel2.ClientSize.Height.Should().Be(fixedContractHeight + 79);
        contractDetails.ClientSize.Height.Should().Be(fixedContractHeight);
        historyList.Height.Should().BeGreaterThan(initialHistoryListHeight);
        graphTabs.Height.Should().BeGreaterThan(initialGraphHeight);
        realTimeData.Panel2.ClientSize.Height.Should().Be(169);
        (realTimeData.Panel2.ClientSize.Height - realTimeHeader.Bottom).Should().BeLessThanOrEqualTo(10);
        spreadRows.Bottom.Should().BeLessThanOrEqualTo(realTimeHeader.ClientSize.Height);
        new[]
            {
                Field<TextBox>(ironCondor, "txtPutSpreadType"),
                Field<TextBox>(ironCondor, "txtCallSpreadType"),
            }
            .Should().OnlyContain(control => control.Visible);

        host.Size = new Size(800, 600);
        ((IFormControl)ironCondor).Resize(host);
        realTimeData.Visible.Should().BeTrue();
        graphTabs.Visible.Should().BeFalse();

        host.Size = Size.Empty;
        ((IFormControl)ironCondor).Resize(host);
        graphTabs.Visible.Should().BeFalse();
    }

    static bool ChartUsesTradeTypography(System.Windows.Forms.DataVisualization.Charting.Chart chart)
        => chart.ChartAreas.Cast<System.Windows.Forms.DataVisualization.Charting.ChartArea>()
               .SelectMany(area => new[] { area.AxisX, area.AxisX2, area.AxisY, area.AxisY2 })
               .All(axis => IsTradeFont(axis.LabelStyle.Font) && IsTradeFont(axis.TitleFont))
           && chart.Legends.Cast<System.Windows.Forms.DataVisualization.Charting.Legend>()
               .All(legend => IsTradeFont(legend.Font))
           && chart.Titles.Cast<System.Windows.Forms.DataVisualization.Charting.Title>()
               .All(title => IsTradeFont(title.Font))
           && chart.Series.Cast<System.Windows.Forms.DataVisualization.Charting.Series>()
               .All(series => IsTradeFont(series.Font)
                   && series.Points.All(point => IsTradeFont(point.Font)));

    static bool IsTradeFont(Font font)
        => font.Name == "Microsoft Sans Serif" && Math.Abs(font.Size - 10F) < 0.01F;

    [Fact]
    [Trait("Gate", "PF-31")]
    [Trait("Category", "PortfolioLegacyHistory")]
    public async Task Selecting_legacy_trade_embeds_original_order_editor_and_missing_TradeDb_shows_only_unavailable_message()
    {
        var root = Substitute.For<IAppRoot>();
        var services = Substitute.For<IUiServiceCatalog>();
        var queries = Substitute.For<IPortfolioQueryApi>();
        var commandResponses = new CommandResponseEventService(Substitute.For<ICommandResponseUIEventConsumer>());
        commandResponses.SetSiteId(Guid.NewGuid());
        root.Services.Returns(services);
        services.PortfolioQueries.Returns(queries);
        services.CommandResponses.Returns(commandResponses);
        var portfolio = Portfolio(1101, "Legacy Test Portfolio") with { OperatingState = PortfolioOperatingState.Draft };
        var mapping = Fund(1101, 5001, "Imported Legacy Fund") with
        {
            OperatingState = FundOperatingState.Draft,
            HistoricalSource = "FundLegacyDb",
            HistoricalSourceFundId = 1004,
        };
        var legacyFund = new FundReadModel(1004, "Imported Legacy Fund", "history", 0m, false, DateTime.UtcNow, "legacy");
        var legacyOrder = new FundOrderReadModel(1004, 1084, DateTime.UtcNow, TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Open, "ES",
            new DateOnly(2024, 1, 2), new DateOnly(2024, 2, 2), "history", DateTime.UtcNow, "legacy", null, string.Empty);
        var orderHistory = new LegacyFundOrderHistoryReadModel { Order = legacyOrder, CompositionTradeCount = 1 };
        var composition = new FundOrderTradeReadModel
        {
            FundId = 1004, OrderId = 1084, TradeId = 1090, TradeType = TradeType.ShortIronCondor,
            BaseContractSymbol = "ES", TradeDate = new DateOnly(2024, 1, 2), MaturityDate = new DateOnly(2024, 2, 2),
        };
        var tradeDb = new OptionTradeReadModel
        {
            OrderId = 1084, TradeId = 1090, TradeType = TradeType.ShortIronCondor,
            TradeDate = new DateOnly(2024, 1, 2), MaturityDate = new DateOnly(2024, 2, 2),
        };
        var history = new LegacyFundTradeHistoryReadModel
        {
            Composition = composition, TradeDbTrade = tradeDb, MatchStatus = LegacyTradeMatchStatus.DefinitionOnly,
        };
        queries.GetLegacyPortfolioScopesAsync(Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<LegacyPortfolioScopeReadModel[]>([new() { Portfolio = portfolio, Funds = [mapping] }]));
        queries.GetLegacyFundCatalogAsync(Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<LegacyFundHistoryReadModel[]>([new() { Fund = legacyFund, OrderCount = 1, CompositionTradeCount = 1 }]));
        queries.GetLegacyFundOrdersAsync(1004, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), 1000, Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<LegacyFundOrderHistoryReadModel[]>([orderHistory]));
        FuturesContractV3ReadModel[] contracts =
        [
            new()
            {
                ContractId = "ES20240920",
                Symbol = "ES",
                LastTradeDate = new DateOnly(2024, 9, 20)
            }
        ];
        var vm = new TradeOrderEditorViewModel(root, new DateOnly(2026, 8, 30), contracts, Substitute.For<IReferenceDataService>());
        vm.SetOrderDateRange(new DateTime(2000, 1, 1), new DateTime(2026, 9, 1));
        await vm.SetLegacyHistoryModeAsync(true);
        using var form = new TradeOrderEditorForm(root, Substitute.For<IReferenceDataService>());
        form.LoadViewModel(vm);
        SetField(form, "_selectedLegacyOrder", orderHistory);

        await InvokeTask(form, "ShowLegacyTradeEditorAsync", history);

        var panel = Field<Panel>(form, "pnlTradeControl");
        panel.Controls.Cast<Control>().Should().ContainSingle().Which.Should().BeOfType<IronCondorTradeOrderView>();
        var editor = (IronCondorTradeOrderView)panel.Controls[0];
        editor.IsHistoricalReadOnly.Should().BeTrue();
        ControlsAndSelf(editor)
            .Where(control => control is TextBox or ComboBox or NumericUpDown or DateTimePicker)
            .Should().OnlyContain(control =>
                control.BackColor.ToArgb() == Color.Black.ToArgb()
                && control.ForeColor.ToArgb() == Color.White.ToArgb());
        ControlsAndSelf(editor).OfType<DateTimePicker>().Should().OnlyContain(date =>
            date.CalendarMonthBackground.ToArgb() == Color.Black.ToArgb()
            && date.CalendarForeColor.ToArgb() == Color.White.ToArgb()
            && date.CalendarTitleBackColor.ToArgb() == Color.Black.ToArgb()
            && date.CalendarTitleForeColor.ToArgb() == Color.White.ToArgb());
        ControlsAndSelf(editor).OfType<ComboBox>().Should().OnlyContain(combo =>
            combo.DrawMode == DrawMode.OwnerDrawFixed);
        var orderType = Field<ComboBox>(editor, "ddlOrderType");
        Field<ComboBox>(editor, "ddlPrice").DropDownStyle.Should().Be(ComboBoxStyle.DropDownList);
        var dateControls = ControlsAndSelf(editor).OfType<DateTimePicker>().ToArray();
        dateControls.Should().OnlyContain(date => date.GetType().Name == "DarkDateTimePicker");
        var readOnlyDate = dateControls[0];
        readOnlyDate.Value = new DateTime(2024, 2, 2);
        readOnlyDate.Enabled = false;
        using (var renderedDate = new Bitmap(readOnlyDate.Width, readOnlyDate.Height))
        using (var graphics = Graphics.FromImage(renderedDate))
        {
            graphics.Clear(Color.Magenta);
            readOnlyDate.GetType()
                .GetMethod("DrawDarkSurface", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(readOnlyDate, [graphics]);
            var dateSurface = Enumerable.Range(0, renderedDate.Width)
                .SelectMany(x => Enumerable.Range(0, renderedDate.Height)
                    .Select(y => renderedDate.GetPixel(x, y)))
                .ToArray();
            dateSurface.Count(color => color.GetBrightness() < 0.08F)
                .Should().BeGreaterThan(dateSurface.Length / 2,
                    "the complete read-only date surface must be black");
            dateSurface.Should().Contain(color =>
                Math.Abs(color.R - color.G) <= 2
                && Math.Abs(color.G - color.B) <= 2
                && color.R >= 80
                && color.R <= 200,
                "read-only date text and arrow must be gray");
        }
        orderType.Items.Clear();
        orderType.Items.Add("Limit");
        orderType.SelectedIndex = 0;
        orderType.Enabled = false;
        using (var renderedOrderType = new Bitmap(orderType.Width, orderType.Height))
        using (var graphics = Graphics.FromImage(renderedOrderType))
        {
            graphics.Clear(Color.Magenta);
            var drawArgs = new DrawItemEventArgs(
                graphics,
                orderType.Font,
                new Rectangle(0, 0, renderedOrderType.Width, renderedOrderType.Height),
                0,
                DrawItemState.Disabled);
            typeof(IronCondorTradeOrderView)
                .GetMethod("DrawBlackComboBoxItem", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, [orderType, drawArgs]);
            var inputSurface = Enumerable.Range(0, renderedOrderType.Width)
                .SelectMany(x => Enumerable.Range(0, renderedOrderType.Height)
                    .Select(y => renderedOrderType.GetPixel(x, y)))
                .ToArray();
            inputSurface.Count(color => color.GetBrightness() < 0.08F)
                .Should().BeGreaterThan(inputSurface.Length / 2,
                    "the rendered disabled dropdown surface must remain black");
            inputSurface.Should().Contain(color =>
                Math.Abs(color.R - color.G) <= 2
                && Math.Abs(color.G - color.B) <= 2
                && color.R >= 80
                && color.R <= 200,
                "the rendered disabled dropdown text must be gray");
        }
        var legGrid = Field<TableLayoutPanel>(editor, "pnlTradeStrategy");
        var riskGrid = Field<TableLayoutPanel>(editor, "tableLayoutPanel1");
        var fundBalance = Field<TextBox>(editor, "txtFundBalance");
        legGrid.Dock.Should().Be(DockStyle.Top);
        legGrid.Width.Should().Be(editor.ClientSize.Width);
        legGrid.Height.Should().Be(160);
        riskGrid.Bottom.Should().BeLessThanOrEqualTo(editor.ClientSize.Height);
        var fundBalanceBottom = riskGrid.Top + fundBalance.Bottom;
        (editor.ClientSize.Height - fundBalanceBottom).Should().BeGreaterThanOrEqualTo(6);
        panel.Height.Should().Be(editor.MinimumSize.Height);
        panel.Bottom.Should().BeLessThanOrEqualTo(
            Field<Panel>(form, "pnlTradePosition").ClientSize.Height);
        (Field<Panel>(form, "pnlTradePosition").ClientSize.Height - panel.Bottom)
            .Should().BeGreaterThanOrEqualTo(12);
        ControlsAndSelf(editor).Skip(1).Should().OnlyContain(control =>
            BottomWithin(control, editor) <= editor.ClientSize.Height);
        riskGrid.Controls.Cast<Control>().Should().OnlyContain(control =>
            FitsWithinTableCell(riskGrid, control));
        legGrid.ColumnStyles.Cast<ColumnStyle>().Should().OnlyContain(column =>
            column.SizeType == SizeType.Percent && Math.Abs(column.Width - 10F) < 0.01F);
        var legColumnWidths = legGrid.GetColumnWidths();
        (legColumnWidths.Max() - legColumnWidths.Min()).Should().BeLessThanOrEqualTo(10);
        new[] { "panel2", "panel3", "panel9", "panel10" }
            .Select(name => Field<Panel>(editor, name))
            .Should().OnlyContain(panel =>
                panel.Margin.Top == 2
                && panel.Margin.Bottom == 2
                && panel.Controls.Cast<Control>().All(control => control.Bottom <= panel.ClientSize.Height));
        var leg1NetSpread = Field<Control>(editor, "txtLeg1NetSpread");
        new[]
            {
                "txtLeg1ExpectedOTMProbability", "txtLeg1ActualOTMProbability",
                "txtLeg1MaxLossLimit", "txtLeg1MinProfitLimit",
            }
            .Select(name => Field<Control>(editor, name))
            .Should().OnlyContain(control =>
                TopWithin(control, editor) == TopWithin(leg1NetSpread, editor)
                && BottomWithin(control, editor) == BottomWithin(leg1NetSpread, editor));
        var leg3NetSpread = Field<Control>(editor, "txtLeg3NetSpread");
        new[]
            {
                "txtLeg3ExpectedOTMProbability", "txtLeg3ActualOTMProbability",
                "txtLeg3MaxLossLimit", "txtLeg3MinProfitLimit",
            }
            .Select(name => Field<Control>(editor, name))
            .Should().OnlyContain(control =>
                TopWithin(control, editor) == TopWithin(leg3NetSpread, editor)
                && BottomWithin(control, editor) == BottomWithin(leg3NetSpread, editor));
        new[]
            {
                "lblAction", "lblLastTradeDate", "lblStrikePrice", "lblOptionType",
                "lblBid", "lblAsk", "lblNetSpread", "lblSpread", "lblTradeValue",
                "label4", "lblOTMProbability", "label5", "label6", "lblTradeLimits",
                "label7", "label8",
            }
            .Select(name => Field<Label>(editor, name))
            .Should().OnlyContain(label => label.TextAlign == ContentAlignment.MiddleCenter);

        await InvokeTask(form, "ShowLegacyTradeEditorAsync", history with { TradeDbTrade = null });

        panel.Controls.Cast<Control>().Should().ContainSingle().Which.Should().BeOfType<Label>();
        panel.Controls[0].Text.Should().Be("No corresponding TradeDb trade exists for 1084:1090.");
        panel.Controls[0].Text.Should().NotContain("LEGACY COMPOSITION");
        await vm.DisposeAsync();
    }

    [Fact]
    [Trait("Gate", "PF-31")]
    [Trait("Category", "PortfolioLegacyHistory")]
    public async Task Legacy_mode_uses_only_legacy_queries_and_disables_every_trade_mutation()
    {
        var root = Substitute.For<IAppRoot>();
        var services = Substitute.For<IUiServiceCatalog>();
        var queries = Substitute.For<IPortfolioQueryApi>();
        root.Services.Returns(services);
        services.PortfolioQueries.Returns(queries);
        var portfolio = Portfolio(1101, "Legacy Test Portfolio") with { OperatingState = PortfolioOperatingState.Draft };
        var mapping = Fund(1101, 5001, "Imported Legacy Fund") with
        {
            OperatingState = FundOperatingState.Draft,
            HistoricalSource = "FundLegacyDb",
            HistoricalSourceFundId = 1004,
        };
        var legacyFund = new FundReadModel(1004, "Imported Legacy Fund", "history", 0m, false, DateTime.UtcNow, "legacy");
        var legacyOrder = new FundOrderReadModel(1004, 1084, DateTime.UtcNow, TomasAI.IFM.Domain.Fund.Shared.OrderStatus.Open, "ES",
            new DateOnly(2024, 1, 2), new DateOnly(2024, 2, 2), "history", DateTime.UtcNow, "legacy", null, string.Empty);
        var composition = new FundOrderTradeReadModel { FundId = 1004, OrderId = 1084, TradeId = 1090, TradeType = TradeType.ShortIronCondor };
        queries.GetLegacyPortfolioScopesAsync(Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<LegacyPortfolioScopeReadModel[]>([new() { Portfolio = portfolio, Funds = [mapping] }]));
        queries.GetLegacyFundCatalogAsync(Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<LegacyFundHistoryReadModel[]>([new() { Fund = legacyFund, OrderCount = 1, CompositionTradeCount = 1 }]));
        queries.GetLegacyFundOrdersAsync(1004, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), 1000, Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<LegacyFundOrderHistoryReadModel[]>([new() { Order = legacyOrder, CompositionTradeCount = 1 }]));
        queries.GetLegacyFundOrderTradesAsync(1004, 1084, Arg.Any<CancellationToken>()).Returns(
            new ServiceOk<LegacyFundTradeHistoryReadModel[]>([new() { Composition = composition, MatchStatus = LegacyTradeMatchStatus.NoTradeDbDefinition }]));
        var vm = new TradeOrderEditorViewModel(root, new DateOnly(2026, 8, 30), [], Substitute.For<IReferenceDataService>());
        vm.SetOrderDateRange(new DateTime(2000, 1, 1), DateTime.Today.AddDays(1));

        await vm.SetLegacyHistoryModeAsync(true);
        var trades = await vm.GetLegacyTradesAsync(1084);

        vm.IsLegacyHistoryMode.Should().BeTrue();
        vm.SelectedPortfolio!.Name.Should().Be("Legacy Test Portfolio");
        vm.SelectedFund!.FundId.Should().Be(1004);
        vm.LegacyOrders.Should().ContainSingle();
        trades.Should().ContainSingle();
        vm.CanCreateOrder.Should().BeFalse();
        vm.CanAddTrade.Should().BeFalse();
        vm.CanSubmitOrder.Should().BeFalse();
        await queries.DidNotReceiveWithAnyArgs().GetOrdersAsync(default, default, default, default, default, default);
    }

    [Fact]
    [Trait("Gate", "PF-21")]
    [Trait("Gate", "PF-28")]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public void Obsolete_separate_composition_viewer_is_not_present()
    {
        var assembly = typeof(TradeOrderEditorForm).Assembly;
        assembly.GetType("TomasAI.IFM.UI.Net.Views.Portfolio.PortfolioCompositionForm").Should().BeNull();
    }

    [Fact]
    [Trait("Gate", "PF-28")]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public async Task Late_Portfolio_response_cannot_replace_the_newer_scope()
    {
        var root = Substitute.For<IAppRoot>();
        var services = Substitute.For<IUiServiceCatalog>();
        var queries = Substitute.For<IPortfolioQueryApi>();
        root.Services.Returns(services);
        services.PortfolioQueries.Returns(queries);
        var p1 = Portfolio(101, "Portfolio A");
        var p2 = Portfolio(102, "Portfolio B");
        queries.GetPortfoliosAsync(Arg.Any<PortfolioOperatingState?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ServiceResult<PortfolioPage<PortfolioReadModel>>>(OkPage([p1, p2])));
        var delayed = new TaskCompletionSource<ServiceResult<PortfolioPage<FundMandateReadModel>>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var p2Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var p1Calls = 0;
        queries.GetFundsAsync(Arg.Any<int>(), Arg.Any<FundOperatingState?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var portfolioId = call.ArgAt<int>(0);
                if (portfolioId == 102) { p2Started.TrySetResult(); return delayed.Task; }
                p1Calls++;
                return Task.FromResult<ServiceResult<PortfolioPage<FundMandateReadModel>>>(OkPage([Fund(101, p1Calls == 1 ? 201 : 211, p1Calls == 1 ? "Initial A" : "Latest A")]));
            });
        queries.GetOrdersAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<DateOnly>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ServiceResult<PortfolioPage<FundOrderProjectionReadModel>>>(OkPage<FundOrderProjectionReadModel>([])));
        var vm = new TradeOrderEditorViewModel(root, new DateOnly(2026, 8, 30), [], Substitute.For<IReferenceDataService>());
        await vm.LoadFunds();

        var oldScope = vm.SelectPortfolioAsync(1);
        await p2Started.Task;
        var newScope = vm.SelectPortfolioAsync(0);
        await newScope;
        delayed.SetResult(OkPage([Fund(102, 301, "Late B")]));
        await oldScope;

        vm.SelectedPortfolio!.PortfolioId.Should().Be(101);
        vm.Funds.Should().ContainSingle().Which.Name.Should().Be("Latest A");
        vm.Funds.Should().NotContain(x => x.Name == "Late B");
    }

    static PortfolioReadModel Portfolio(int id, string name) => new()
    {
        PortfolioId = id, PortfolioVersion = 1, Name = name, OperatingState = PortfolioOperatingState.Active,
        EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        CreatedOnUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "test",
    };

    static FundMandateReadModel Fund(int portfolioId, int fundId, string name) => new()
    {
        PortfolioId = portfolioId, FundId = fundId, FundMandateVersion = 1, FundCode = $"F{fundId}", Name = name,
        TradingYear = 2026, OperatingState = FundOperatingState.Active, DecisionHorizon = "Daily", Objective = "test",
        UnderlyingUniverse = ["ES"], EligibleAssetTypes = ["Futures"], PermittedTradeFamilies = ["Futures"],
        EffectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        CreatedOnUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), CreatedBy = "test",
    };

    static ServiceResult<PortfolioPage<T>> OkPage<T>(T[] items) where T : class =>
        new ServiceOk<PortfolioPage<T>>(new() { Items = items, PageSize = 200 });

    static T Field<T>(object owner, string name)
    {
        for (var type = owner.GetType(); type is not null; type = type.BaseType)
            if (type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner) is T value)
                return value;
        throw new InvalidOperationException($"Missing {name} on {owner.GetType().Name}.");
    }

    static void SetField(object owner, string name, object? value)
    {
        for (var type = owner.GetType(); type is not null; type = type.BaseType)
            if (type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic) is { } field)
            {
                field.SetValue(owner, value);
                return;
            }
        throw new InvalidOperationException($"Missing {name} on {owner.GetType().Name}.");
    }

    static Task InvokeTask(object owner, string name, params object[] arguments)
        => (Task)(owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(owner, arguments)
            ?? throw new InvalidOperationException($"Missing {name} on {owner.GetType().Name}."));

    static void Invoke(object owner, string name, params object[] arguments)
    {
        var method = owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing {name} on {owner.GetType().Name}.");
        method.Invoke(owner, arguments);
    }

    sealed class HistoricalTradeViewerStub : UserControl, IFormControl
    {
        public int OpenCount { get; private set; }
        public void Open() => OpenCount++;
        public void Resize(Control parentControl) => Size = parentControl.Size;
        public void Close() { }
    }
}
