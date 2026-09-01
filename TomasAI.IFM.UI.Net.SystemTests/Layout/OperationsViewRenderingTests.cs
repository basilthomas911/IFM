using FluentAssertions;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.UI.Net.Views.App;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class OperationsViewRenderingTests
{
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
        var eventList = operations.Controls.Find("lstItiEvents", true)
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
            .Should().Equal("Time", "Change", "Trend", "Price");
        eventList.Columns[0].Width.Should().BeGreaterThanOrEqualTo(185);

    }

    [Fact]
    public void StrategyComposesChartAndHistoryAbovePropertyGrid()
    {
        using var operations = new OperationsView();
        var chart = operations.Controls.Find("itiChart", true)
            .OfType<Chart>()
            .Single();
        var history = operations.Controls.Find("lstItiEvents", true)
            .OfType<ListView>()
            .Single();
        var propertyGrid = operations.Controls.Find("itiPropertyGrid", true)
            .OfType<PropertyGrid>()
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
        detailSplitter.Panel2.Controls.Cast<Control>().Should().Contain(propertyGrid);
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
