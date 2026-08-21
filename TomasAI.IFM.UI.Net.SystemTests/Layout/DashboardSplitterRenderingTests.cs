using FluentAssertions;
using NSubstitute;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.App;
using TomasAI.IFM.UI.Net.Views.App;

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
    public void MarketFeedButtonKeepsBlackBackgroundForEveryHealthState()
    {
        var colorMethod = typeof(IFMAppView).GetMethod(
            "MarketDataFeedColors",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var expectedForegrounds = new Dictionary<MarketDataFeedHealthState, Color>
        {
            [MarketDataFeedHealthState.Inactive] = Color.DarkRed,
            [MarketDataFeedHealthState.Healthy] = Color.LimeGreen,
            [MarketDataFeedHealthState.Intermittent] = Color.Yellow,
            [MarketDataFeedHealthState.Failed] = Color.Orange,
            [MarketDataFeedHealthState.Critical] = Color.Red,
            [MarketDataFeedHealthState.OutsidePositionEntryWindow] = Color.Gray
        };

        foreach (var expected in expectedForegrounds)
        {
            var colors = ((Color Background, Color Foreground))colorMethod.Invoke(
                null,
                [expected.Key])!;
            colors.Background.ToArgb().Should().Be(Color.Black.ToArgb());
            colors.Foreground.ToArgb().Should().Be(expected.Value.ToArgb());
        }
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
                    Substitute.For<IViewNavigator>())
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
            Substitute.For<IViewNavigator>())
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
