using FluentAssertions;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.App;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.Views.App;
using TomasAI.IFM.UI.Net.Views.Presentation;
using TomasAI.IFM.UI.Net.Views.Strategy;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class OperationsViewRenderingTests
{
    [Fact]
    public void WorkflowDetailsAccordion_RetainsControlsForSameRevisionAndRebuildsForNewRevision()
    {
        using var accordion = new StrategyWorkflowDetailsAccordion();
        var workflowId = new TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity.StrategyWorkflowId(Guid.NewGuid());
        var details = new StrategyWorkflowDetails(
            workflowId,
            4,
            "Workflow revision 4",
            [new("iti", "ITI Signal", "Received", StrategyWorkflowDetailState.Completed, "received", "signal")]);

        accordion.Bind(details);
        var content = accordion.Controls.OfType<FlowLayoutPanel>().Single();
        var originalControls = content.Controls.Cast<Control>().ToArray();

        accordion.Bind(details with { Header = "Equivalent revision" });

        content.Controls.Cast<Control>().Should().Equal(originalControls);

        accordion.Bind(details with { WorkflowRevision = 5, Header = "Workflow revision 5" });

        content.Controls.Cast<Control>().Should().NotEqual(originalControls);
        content.Controls.OfType<Label>().Single().Text.Should().Be("Workflow revision 5");
    }

    [Fact]
    public void StrategyProvidesDailyDefaultTimeFrameSelectorAndFullTimestampColumn()
    {
        using var operations = new OperationsView();
        var selector = operations.Controls.Find("ddlTimeFrame", true)
            .OfType<ComboBox>()
            .Single();
        var selectorLabel = operations.Controls.Find("lblTimeFrame", true)
            .OfType<Label>()
            .Single();
        var eventList = operations.Controls.Find("lstStrategyWorkflows", true)
            .OfType<ListView>()
            .Single();

        selector.DropDownStyle.Should().Be(ComboBoxStyle.DropDownList);
        selector.Items.Cast<TimeFrameType>().Should().Equal(
            TimeFrameType.Daily,
            TimeFrameType.Weekly,
            TimeFrameType.Monthly);
        selector.SelectedItem.Should().Be(TimeFrameType.Daily);
        selectorLabel.Text.Should().Be("Time Frame:");
        selectorLabel.Font.Size.Should().Be(selector.Font.Size);
        selectorLabel.Top.Should().Be(selector.Top);
        selectorLabel.Height.Should().Be(selector.Height);
        selectorLabel.Width.Should().BeGreaterThanOrEqualTo(selectorLabel.PreferredWidth);
        eventList.Columns.Cast<ColumnHeader>().Select(column => column.Text)
            .Should().Equal(
                "Date/Time",
                "Futures ITI Signal Event",
                "Trend Type",
                "Futures Price",
                "Pipeline State",
                "Workflow End State");
        eventList.Columns[0].Width.Should().BeGreaterThanOrEqualTo(185);

    }

    [Fact]
    public void StrategyComposesChartAndWorkflowListAboveDetailsAndSummaryTabs()
    {
        using var operations = new OperationsView();
        var chart = operations.Controls.Find("itiChart", true)
            .OfType<Chart>()
            .Single();
        var history = operations.Controls.Find("lstStrategyWorkflows", true)
            .OfType<ListView>()
            .Single();
        var workflowTabs = operations.Controls.Find("workflowTabs", true)
            .OfType<TabControl>()
            .Single();
        var contentSplitter = operations.Controls.Find("strategyContentSplitter", true)
            .OfType<SplitContainer>()
            .Single();
        var detailSplitter = operations.Controls.Find("strategySplitter", true)
            .OfType<SplitContainer>()
            .Single();

        contentSplitter.Orientation.Should().Be(Orientation.Horizontal);
        contentSplitter.Panel1.Controls.Cast<Control>().Should().Contain(chart);
        contentSplitter.Panel2.Controls.Cast<Control>().Should().Contain(history);
        detailSplitter.Panel1.Controls.Cast<Control>().Should().Contain(contentSplitter);
        detailSplitter.Panel2.Controls.Cast<Control>().Should().Contain(workflowTabs);
        workflowTabs.TabPages.Cast<TabPage>().Select(page => page.Text)
            .Should().Equal("Details", "Summary");
        operations.Controls.Find("lblWorkflowSummaryUnavailable", true)
            .OfType<Label>().Single().Text.Should().Be("Summary is not available.");
        chart.ChartAreas.Single().AxisX.Title.Should().Be("Market Time (ET)");
        chart.ChartAreas.Single().AxisY.Title.Should().Be("ITI Signal Price");
        chart.Titles.Should().BeEmpty();
        chart.Font.Name.Should().Be("Microsoft Sans Serif");
        chart.Font.Size.Should().BeApproximately(10F, 0.01F);
        chart.ChartAreas.Cast<ChartArea>()
            .SelectMany(area => new[] { area.AxisX, area.AxisX2, area.AxisY, area.AxisY2 })
            .Should().OnlyContain(axis =>
                axis.LabelStyle.Font.Name == "Microsoft Sans Serif"
                && Math.Abs(axis.LabelStyle.Font.Size - 10F) < 0.01F
                && axis.TitleFont.Name == "Microsoft Sans Serif"
                && Math.Abs(axis.TitleFont.Size - 10F) < 0.01F);
        chart.Legends.Should().OnlyContain(legend =>
            legend.Font.Name == "Microsoft Sans Serif"
            && Math.Abs(legend.Font.Size - 10F) < 0.01F);
        chart.Series.Should().OnlyContain(series =>
            series.Font.Name == "Microsoft Sans Serif"
            && Math.Abs(series.Font.Size - 10F) < 0.01F);
        chart.Series["Other ITI Event"].Color.ToArgb().Should().Be(Color.Navy.ToArgb());
        chart.Series.Select(series => series.Name).Should().Contain(
        [
            "ITI Price",
            "Other ITI Event",
            "Direction Up",
            "Direction Down",
            "Selection"
        ]);
    }

    [Theory]
    [InlineData(PipelineActorDisplayState.Processing, "Yellow")]
    [InlineData(PipelineActorDisplayState.Continued, "Lime")]
    [InlineData(PipelineActorDisplayState.Stopped, "Red")]
    public void StrategyPipelineStatesUseApprovedBrightColors(
        PipelineActorDisplayState state,
        string expectedColorName)
    {
        var color = (Color)typeof(OperationsView)
            .GetMethod("PipelineColor", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [state])!;

        color.Name.Should().Be(expectedColorName);
    }

    [Fact]
    public void StrategyPipelineOwnerDrawingRendersEveryApprovedCircleColor()
    {
        using var operations = new OperationsView();
        using var bitmap = new Bitmap(400, 40);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);
        PipelineActorIndicator[] actors =
        [
            new(StrategyWorkflowStage.RegimeDiscovery, "RD", "Regime Discovery", PipelineActorDisplayState.Continued, "Regime Discovery completed"),
            new(StrategyWorkflowStage.MarketCondition, "MC", "Market Condition", PipelineActorDisplayState.Processing, "Market Condition processing"),
            new(StrategyWorkflowStage.TradeSelection, "TS", "Trade Selection", PipelineActorDisplayState.Stopped, "Trade Selection failed")
        ];

        typeof(OperationsView)
            .GetMethod("DrawPipelineActors", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(operations, [graphics, new Rectangle(0, 0, bitmap.Width, bitmap.Height), actors, false]);

        var pixels = Enumerable.Range(0, bitmap.Width)
            .SelectMany(x => Enumerable.Range(0, bitmap.Height).Select(y => bitmap.GetPixel(x, y).ToArgb()))
            .ToArray();
        pixels.Count(value => value == Color.Lime.ToArgb()).Should().BeGreaterThan(20);
        pixels.Count(value => value == Color.Yellow.ToArgb()).Should().BeGreaterThan(20);
        pixels.Count(value => value == Color.Red.ToArgb()).Should().BeGreaterThan(20);
    }

    [Fact]
    public void FailedRegimeDiscoveryRendersOneBrightRedCircleWithoutCellText()
    {
        using var operations = new OperationsView();
        var actor = new PipelineActorIndicator(
            StrategyWorkflowStage.RegimeDiscovery,
            "RD",
            "Regime Discovery",
            PipelineActorDisplayState.Stopped,
            "Regime Discovery failed");
        var row = new StrategyWorkflowRow(
            default,
            default!,
            1,
            DateTime.UtcNow,
            TimeFrameType.Daily,
            default,
            default,
            0,
            "failed-regime-discovery",
            default,
            default,
            "Pipeline Failed",
            [actor]);

        typeof(OperationsView)
            .GetMethod("RenderWorkflows", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(operations, new object[] { new[] { row } });

        var list = operations.Controls.Find("lstStrategyWorkflows", true).OfType<ListView>().Single();
        list.Items.Count.Should().Be(1);
        list.Items[0].SubItems[4].Text.Should().BeEmpty();
        list.Items[0].ToolTipText.Should().Contain("Regime Discovery failed");

        DarkTradingTheme.Apply(operations);
        using var bitmap = new Bitmap(80, 40);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);
        var paint = new DrawListViewSubItemEventArgs(
            graphics,
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            list.Items[0],
            list.Items[0].SubItems[4],
            0,
            4,
            list.Columns[4],
            ListViewItemStates.Default);
        typeof(DarkTradingTheme)
            .GetMethod("DrawListSubItem", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [list, paint]);

        var redPixels = Enumerable.Range(0, bitmap.Width)
            .SelectMany(x => Enumerable.Range(0, bitmap.Height).Select(y => (X: x, Y: y, Color: bitmap.GetPixel(x, y))))
            .Where(pixel => pixel.Color.ToArgb() == Color.Red.ToArgb())
            .ToArray();
        redPixels.Should().HaveCountGreaterThan(20);
        (redPixels.Max(pixel => pixel.X) - redPixels.Min(pixel => pixel.X)).Should().BeLessThanOrEqualTo(12);
    }

    [Fact]
    public void StrategyWorkflowSelectionHighlightsOnlyAMatchingChartPoint()
    {
        using var operations = new OperationsView();
        var chart = operations.Controls.Find("itiChart", true).OfType<Chart>().Single();
        var point = chart.Series["ITI Price"].Points.AddXY(1d, 6500d);
        chart.Series["ITI Price"].Points[point].Tag = "signal-identity";
        var highlight = typeof(OperationsView)
            .GetMethod("HighlightChartPoint", BindingFlags.Instance | BindingFlags.NonPublic)!;

        highlight.Invoke(operations, ["signal-identity"]);
        chart.Series["Selection"].Points.Should().ContainSingle();
        highlight.Invoke(operations, ["missing-signal"]);
        chart.Series["Selection"].Points.Should().BeEmpty();
    }

    [Theory]
    [InlineData(TimeFrameType.Daily, "09:30:01.250 AM")]
    [InlineData(TimeFrameType.Weekly, "21-Aug-2026 09:30:01.250 AM")]
    [InlineData(TimeFrameType.Monthly, "21-Aug-2026 09:30:01.250 AM")]
    public void StrategyFormatsTimeForSelectedTimeFrame(
        TimeFrameType timeFrame,
        string expected)
    {
        var utcTime = new DateTime(2026, 8, 21, 13, 30, 1, 250, DateTimeKind.Utc);
        var formatted = (string)typeof(OperationsView)
            .GetMethod("FormatListTime", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [utcTime, timeFrame])!;

        formatted.Should().Be(expected);
    }

    [Fact]
    public void MainDashboardTabViewsUseBorderlessDarkChrome()
    {
        using var operations = new OperationsView();
        using var calendar = new MarketEconomicCalendarView();

        var tabControls = new[]
        {
            operations.Controls.Find("operationsTabs", true).OfType<TabControl>().Single(),
            calendar.Controls.Find("tabCalendarPeriod", true).OfType<TabControl>().Single()
        };

        tabControls.Should().OnlyContain(tab => tab.GetType().Name == "DarkTabControl");
        calendar.Controls.Find("tabCalendarPeriod", true)
            .OfType<TabControl>()
            .Single()
            .TabPages.Cast<TabPage>()
            .Should().OnlyContain(page =>
                page.BackColor.ToArgb() == Color.Black.ToArgb()
                && !page.UseVisualStyleBackColor);
    }

    [Fact]
    public void MarketDataEsAndVxTabsUseDarkChromeSharedTypographyAndSubtleGridlines()
    {
        using var view = new MarketDataView();
        var tabs = view.Controls.Find("tabMarketData", true)
            .OfType<TabControl>()
            .Single();
        var charts = new[] { "graphES", "graphVIX" }
            .Select(name => view.Controls.Find(name, true).OfType<Chart>().Single())
            .ToArray();

        tabs.GetType().Name.Should().Be("DarkTabControl");
        tabs.Font.Name.Should().Be("Microsoft Sans Serif");
        tabs.Font.Size.Should().BeApproximately(10F, 0.01F);
        tabs.TabPages.Cast<TabPage>().Should().OnlyContain(page =>
            page.BackColor.ToArgb() == Color.Black.ToArgb()
            && !page.UseVisualStyleBackColor);
        charts.Should().OnlyContain(chart =>
            chart.ChartAreas.Single().AxisX.MajorGrid.Enabled
            && chart.ChartAreas.Single().AxisX.MajorGrid.LineColor.ToArgb()
                == Color.FromArgb(45, 45, 45).ToArgb()
            && chart.ChartAreas.Single().AxisY2.MajorGrid.Enabled
            && chart.ChartAreas.Single().AxisY2.MajorGrid.LineColor.ToArgb()
                == Color.FromArgb(45, 45, 45).ToArgb());
        charts.Should().OnlyContain(chart =>
            chart.Font.Name == "Microsoft Sans Serif"
            && Math.Abs(chart.Font.Size - 10F) < 0.01F
            && chart.ChartAreas.Cast<ChartArea>()
                .SelectMany(area => new[] { area.AxisX, area.AxisX2, area.AxisY, area.AxisY2 })
                .All(axis =>
                    axis.LabelStyle.Font.Name == "Microsoft Sans Serif"
                    && Math.Abs(axis.LabelStyle.Font.Size - 10F) < 0.01F
                    && axis.TitleFont.Name == "Microsoft Sans Serif"
                    && Math.Abs(axis.TitleFont.Size - 10F) < 0.01F)
            && chart.Legends.All(legend =>
                legend.Font.Name == "Microsoft Sans Serif"
                && Math.Abs(legend.Font.Size - 10F) < 0.01F)
            && chart.Series.All(series =>
                series.Font.Name == "Microsoft Sans Serif"
                && Math.Abs(series.Font.Size - 10F) < 0.01F));
    }

    [Fact]
    public void MarketDataChart_UsesFullWindowAndExtendsSingleObservationFlat()
    {
        using var view = new MarketDataView();
        var windowStartUtc = new DateTime(2026, 8, 11, 10, 0, 0, DateTimeKind.Utc);
        var windowEndUtc = windowStartUtc.AddHours(6);
        const decimal value = 5_250m;
        var bar = new FuturesBarDataReadModel(
            "ESZ26",
            "ES",
            new DateOnly(2026, 8, 11),
            windowStartUtc,
            BarRateType.FifteenSeconds,
            value,
            0,
            0);

        view.RefreshView(new FuturesBarChartSnapshot(
            "ES",
            windowStartUtc,
            windowEndUtc,
            [bar]));

        var chart = view.Controls.Find("graphES", true).OfType<Chart>().Single();
        var expectedStart = EasternTime.FromUtc(windowStartUtc).ToOADate();
        var expectedEnd = EasternTime.FromUtc(windowEndUtc).ToOADate();
        chart.ChartAreas[0].AxisX.Minimum.Should().BeApproximately(expectedStart, 0.000_000_1);
        chart.ChartAreas[0].AxisX.Maximum.Should().BeApproximately(expectedEnd, 0.000_000_1);
        chart.Series[0].Points.Should().HaveCount(2);
        chart.Series[0].Points[0].XValue.Should().BeApproximately(expectedStart, 0.000_000_1);
        chart.Series[0].Points[1].XValue.Should().BeApproximately(expectedEnd, 0.000_000_1);
        chart.Series[0].Points.Should().OnlyContain(point => point.YValues[0] == (double)value);
    }

    [Fact]
    public async Task TabChromeRendersBlackInsteadOfSystemWindowWhite()
    {
        var completion = new TaskCompletionSource<TabChromeRendering>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var controlType = typeof(OperationsView).Assembly.GetType(
                    "TomasAI.IFM.UI.Net.Views.App.DarkTabControl",
                    throwOnError: true)!;
                using var tabs = (TabControl)Activator.CreateInstance(
                    controlType,
                    nonPublic: true)!;
                tabs.Size = new Size(527, 796);
                tabs.TabPages.AddRange(
                [
                    new TabPage("Strategy") { BackColor = Color.Black },
                    new TabPage("Latency") { BackColor = Color.Black },
                    new TabPage("Traffic") { BackColor = Color.Black },
                    new TabPage("Errors") { BackColor = Color.Black },
                    new TabPage("Saturation") { BackColor = Color.Black }
                ]);
                tabs.CreateControl();
                tabs.PerformLayout();

                using var bitmap = new Bitmap(tabs.Width, tabs.Height);
                using var graphics = Graphics.FromImage(bitmap);
                using var paintArgs = new PaintEventArgs(graphics, tabs.ClientRectangle);
                controlType
                    .GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(tabs, [paintArgs]);
                var lastTab = tabs.GetTabRect(tabs.TabCount - 1);
                var pageBounds = tabs.DisplayRectangle;
                var activeTab = tabs.GetTabRect(0);
                var inactiveTab = tabs.GetTabRect(1);
                completion.SetResult(new TabChromeRendering(
                    [
                        bitmap.GetPixel(tabs.Width - 2, lastTab.Top + (lastTab.Height / 2)),
                        bitmap.GetPixel(1, tabs.Height - 2),
                        bitmap.GetPixel(pageBounds.X - 1, pageBounds.Top + (pageBounds.Height / 2))
                    ],
                    bitmap.GetPixel(activeTab.Left, activeTab.Top + (activeTab.Height / 2)),
                    bitmap.GetPixel(inactiveTab.Left, inactiveTab.Top + (inactiveTab.Height / 2)),
                    (Color)controlType.GetField(
                        "InactiveTabTextColor",
                        BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!,
                    (FontStyle)controlType.GetField(
                        "SelectedTabFontStyle",
                        BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!,
                    (FontStyle)controlType.GetField(
                        "InactiveTabFontStyle",
                        BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var rendering = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        thread.Join(TimeSpan.FromSeconds(10)).Should().BeTrue();
        rendering.BackgroundSamples.Should().OnlyContain(
            color => color.ToArgb() == Color.Black.ToArgb());
        rendering.ActiveHeaderEdge.ToArgb().Should().Be(Color.Gray.ToArgb());
        rendering.InactiveHeaderEdge.ToArgb().Should().Be(Color.Black.ToArgb());
        rendering.InactiveHeaderText.ToArgb().Should().Be(Color.LightGray.ToArgb());
        rendering.SelectedHeaderFontStyle.Should().Be(FontStyle.Bold);
        rendering.InactiveHeaderFontStyle.Should().Be(FontStyle.Regular);
    }

    [Fact]
    public async Task EconomicCalendarActiveHeaderBottomOutlineRemainsVisible()
    {
        var completion = new TaskCompletionSource<Color>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var calendar = new MarketEconomicCalendarView();
                calendar.CreateControl();
                calendar.PerformLayout();
                var tabs = calendar.Controls.Find("tabCalendarPeriod", true)
                    .OfType<TabControl>()
                    .Single();
                tabs.CreateControl();
                tabs.PerformLayout();

                using var bitmap = new Bitmap(tabs.Width, tabs.Height);
                using var graphics = Graphics.FromImage(bitmap);
                using var paintArgs = new PaintEventArgs(graphics, tabs.ClientRectangle);
                tabs.GetType()
                    .GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(tabs, [paintArgs]);

                var activeTab = Rectangle.Intersect(tabs.GetTabRect(0), tabs.ClientRectangle);
                completion.SetResult(bitmap.GetPixel(
                    activeTab.Left + (activeTab.Width / 2),
                    activeTab.Bottom - 1));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var bottomOutline = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        thread.Join(TimeSpan.FromSeconds(10)).Should().BeTrue();
        bottomOutline.ToArgb().Should().Be(Color.Gray.ToArgb());
    }

    sealed record TabChromeRendering(
        Color[] BackgroundSamples,
        Color ActiveHeaderEdge,
        Color InactiveHeaderEdge,
        Color InactiveHeaderText,
        FontStyle SelectedHeaderFontStyle,
        FontStyle InactiveHeaderFontStyle);
}
