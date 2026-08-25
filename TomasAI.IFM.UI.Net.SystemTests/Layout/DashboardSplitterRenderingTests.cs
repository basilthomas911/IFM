using FluentAssertions;
using NSubstitute;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.App;
using TomasAI.IFM.UI.Net.Views.App;
using TomasAI.IFM.UI.Net.Services.Reference;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class DashboardSplitterRenderingTests
{
    [Fact]
    public void DashboardMenuBarUsesSolidBlackChrome()
    {
        using var form = CreateForm();
        var menuBar = form.Controls.Find("toolStrip1", true)
            .OfType<ToolStrip>()
            .Single();

        menuBar.BackColor.ToArgb().Should().Be(Color.Black.ToArgb());
        menuBar.ForeColor.ToArgb().Should().Be(Color.White.ToArgb());
        menuBar.GripStyle.Should().Be(ToolStripGripStyle.Hidden);
        menuBar.Renderer.Should().BeAssignableTo<ToolStripProfessionalRenderer>();
        menuBar.Renderer.GetType().Name.Should().Be("DashboardMenuRenderer");
        var feedButtonIndex = menuBar.Items.IndexOfKey("marketDataFeedButton");
        menuBar.Items[feedButtonIndex + 1].Should().BeOfType<ToolStripLabel>();
        menuBar.Items[feedButtonIndex + 1].Name.Should().Be("marketDataFeedHealthIndicator");

        var textColor = menuBar.Renderer.GetType().GetMethod(
            "NavigationTextColor",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var fontStyle = menuBar.Renderer.GetType().GetMethod(
            "NavigationFontStyle",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        ((Color)textColor.Invoke(null, [false, true])!).ToArgb()
            .Should().Be(Color.LightGray.ToArgb());
        ((Color)textColor.Invoke(null, [true, true])!).ToArgb()
            .Should().Be(Color.White.ToArgb());
        ((FontStyle)fontStyle.Invoke(null, [false])!).Should().Be(FontStyle.Regular);
        ((FontStyle)fontStyle.Invoke(null, [true])!).Should().Be(FontStyle.Bold);

        using var bitmap = new Bitmap(menuBar.ClientSize.Width, menuBar.ClientSize.Height);
        menuBar.DrawToBitmap(bitmap, menuBar.ClientRectangle);
        var backgroundX = menuBar.ClientSize.Width - 2;
        Enumerable.Range(1, Math.Max(0, menuBar.ClientSize.Height - 2))
            .Select(y => bitmap.GetPixel(backgroundX, y))
            .Should().OnlyContain(color => color.ToArgb() == Color.Black.ToArgb());
    }

    [Fact]
    public void MenuBarHasOnePixelFullWidthSeparatorBeforeDashboardSplitters()
    {
        using var form = CreateForm();
        var menuBar = form.Controls.Find("toolStrip1", true)
            .OfType<ToolStrip>()
            .Single();
        var separator = form.Controls.Find("menuBarSeparator", true)
            .OfType<Panel>()
            .Single();
        var splitters = form.Controls.Find("operationViewSplitter", true)
            .OfType<SplitContainer>()
            .Single();

        separator.Dock.Should().Be(DockStyle.Top);
        separator.Height.Should().Be(1);
        separator.Width.Should().Be(form.ClientSize.Width);
        separator.BackColor.ToArgb().Should().Be(Color.Gray.ToArgb());
        separator.Top.Should().Be(menuBar.Bottom);
        splitters.Top.Should().Be(separator.Bottom);
    }

    [Fact]
    public void MainWindowRequestsBlackNativeTitleBarWithWhiteText()
    {
        var constants = typeof(IFMAppView)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(field => field.IsLiteral && field.FieldType == typeof(int))
            .ToDictionary(field => field.Name, field => (int)field.GetRawConstantValue()!);

        constants["DwmUseImmersiveDarkMode"].Should().Be(20);
        constants["DwmUseImmersiveDarkModeBefore20H1"].Should().Be(19);
        constants["DwmCaptionColor"].Should().Be(35);
        constants["DwmTextColor"].Should().Be(36);
    }

    [Fact]
    public void MarketFeedButtonUsesBlackBackgroundAndBrightLifecycleText()
    {
        var colorMethod = typeof(IFMAppView).GetMethod(
            "MarketDataFeedColors",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var expected = new Dictionary<bool, (Color Background, Color Foreground)>
        {
            [false] = (Color.Black, Color.LimeGreen),
            [true] = (Color.Black, Color.Red)
        };

        foreach (var lifecycleState in expected)
        {
            var colors = ((Color Background, Color Foreground))colorMethod.Invoke(
                null,
                [lifecycleState.Key])!;
            colors.Background.ToArgb().Should().Be(lifecycleState.Value.Background.ToArgb());
            colors.Foreground.ToArgb().Should().Be(lifecycleState.Value.Foreground.ToArgb());
        }

        using var form = CreateForm();
        var menuBar = form.Controls.Find("toolStrip1", true).OfType<ToolStrip>().Single();
        var button = menuBar.Items.Find("marketDataFeedButton", false).Single();
        button.Enabled = true;
        button.BackColor = Color.Black;
        button.ForeColor = Color.Red;
        using var bitmap = new Bitmap(menuBar.ClientSize.Width, menuBar.ClientSize.Height);
        menuBar.DrawToBitmap(bitmap, menuBar.ClientRectangle);
        bitmap.GetPixel(button.Bounds.Left + 2, button.Bounds.Top + 2).ToArgb()
            .Should().Be(Color.Black.ToArgb());

        var healthColorMethod = typeof(IFMAppView).GetMethod(
            "MarketDataFeedHealthColors",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var expectedHealthColors = new Dictionary<MarketDataFeedHealthState, (Color Background, Color Foreground)>
        {
            [MarketDataFeedHealthState.Inactive] = (Color.DimGray, Color.White),
            [MarketDataFeedHealthState.Healthy] = (Color.LimeGreen, Color.Black),
            [MarketDataFeedHealthState.Intermittent] = (Color.Yellow, Color.Black),
            [MarketDataFeedHealthState.Failed] = (Color.Red, Color.White),
            [MarketDataFeedHealthState.Critical] = (Color.Red, Color.White),
            [MarketDataFeedHealthState.OutsidePositionEntryWindow] = (Color.SteelBlue, Color.White)
        };
        foreach (var healthState in expectedHealthColors)
        {
            var colors = ((Color Background, Color Foreground))healthColorMethod.Invoke(
                null,
                [healthState.Key])!;
            colors.Background.ToArgb().Should().Be(healthState.Value.Background.ToArgb());
            colors.Foreground.ToArgb().Should().Be(healthState.Value.Foreground.ToArgb());
        }

        var indicator = menuBar.Items.Find("marketDataFeedHealthIndicator", false).Single();
        indicator.BackColor = Color.Yellow;
        indicator.ForeColor = Color.Black;
        menuBar.DrawToBitmap(bitmap, menuBar.ClientRectangle);
        bitmap.GetPixel(indicator.Bounds.Left + 2, indicator.Bounds.Top + 2).ToArgb()
            .Should().Be(Color.Yellow.ToArgb());
    }

    [Fact]
    public async Task StatusBarRendersOneGrayPixelAboveBlackBackground()
    {
        var completion = new TaskCompletionSource<StatusBarRendering>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var form = CreateForm();
                completion.SetResult(CaptureStatusBar(form));
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

        rendering.TopRow.Should().OnlyContain(color => color.ToArgb() == Color.Gray.ToArgb());
        rendering.SecondRow.Should().OnlyContain(color => color.ToArgb() == Color.Black.ToArgb());
    }

    [Fact]
    public async Task DashboardSplittersRenderOneGrayPixelWithinBlackDragArea()
    {
        var completion = new TaskCompletionSource<SplitterRendering[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var appRoot = Substitute.For<IAppRoot>();
                appRoot.AppEnvironment.Returns("TEST");
                using var form = new IFMAppView(
                    appRoot,
                    Substitute.For<IViewNavigator>(),
                    Substitute.For<IReferenceDataService>(),
                    Substitute.For<IEconomicCalendarService>())
                {
                    ClientSize = new Size(1200, 800),
                    WindowState = FormWindowState.Normal
                };
                form.CreateControl();
                form.PerformLayout();
                form.Controls.Find("tabTradeBlotter", true)
                    .OfType<TabControl>()
                    .Single()
                    .GetType().Name.Should().Be("DarkTabControl");

                completion.SetResult(
                [
                    CaptureSplitter(form, "operationViewSplitter"),
                    CaptureSplitter(form, "marketViewSplitter")
                ]);
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var renderings = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        thread.Join(TimeSpan.FromSeconds(10)).Should().BeTrue();

        renderings.Should().OnlyContain(rendering => rendering.SplitterWidth == 5);
        renderings.Should().OnlyContain(rendering => rendering.GrayColumns == 1);
        renderings.Should().OnlyContain(rendering => rendering.BlackColumns == 4);
    }

    static SplitterRendering CaptureSplitter(Control parent, string name)
    {
        var splitter = parent.Controls.Find(name, true).OfType<SplitContainer>().Single();
        splitter.CreateControl();
        splitter.PerformLayout();
        using var bitmap = new Bitmap(splitter.ClientSize.Width, splitter.ClientSize.Height);
        splitter.DrawToBitmap(bitmap, splitter.ClientRectangle);

        var bounds = splitter.SplitterRectangle;
        var sampleY = bounds.Top + (bounds.Height / 2);
        var colors = Enumerable.Range(bounds.Left, bounds.Width)
            .Select(x => bitmap.GetPixel(x, sampleY))
            .ToArray();
        return new SplitterRendering(
            bounds.Width,
            colors.Count(color => color.ToArgb() == Color.Gray.ToArgb()),
            colors.Count(color => color.ToArgb() == Color.Black.ToArgb()));
    }

    static IFMAppView CreateForm()
    {
        var appRoot = Substitute.For<IAppRoot>();
        appRoot.AppEnvironment.Returns("TEST");
        var form = new IFMAppView(
            appRoot,
            Substitute.For<IViewNavigator>(),
            Substitute.For<IReferenceDataService>(),
            Substitute.For<IEconomicCalendarService>())
        {
            ClientSize = new Size(1200, 800),
            WindowState = FormWindowState.Normal
        };
        form.CreateControl();
        form.PerformLayout();
        return form;
    }

    static StatusBarRendering CaptureStatusBar(Control parent)
    {
        var statusBar = parent.Controls.Find("statusBar", true).OfType<StatusStrip>().Single();
        using var bitmap = new Bitmap(statusBar.ClientSize.Width, statusBar.ClientSize.Height);
        using var graphics = Graphics.FromImage(bitmap);
        using var paintArgs = new PaintEventArgs(graphics, statusBar.ClientRectangle);
        statusBar.GetType()
            .GetMethod("OnPaint", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(statusBar, [paintArgs]);
        return new StatusBarRendering(
            Enumerable.Range(0, bitmap.Width).Select(x => bitmap.GetPixel(x, 0)).ToArray(),
            Enumerable.Range(0, bitmap.Width).Select(x => bitmap.GetPixel(x, 1)).ToArray());
    }

    sealed record SplitterRendering(
        int SplitterWidth,
        int GrayColumns,
        int BlackColumns);

    sealed record StatusBarRendering(Color[] TopRow, Color[] SecondRow);
}
