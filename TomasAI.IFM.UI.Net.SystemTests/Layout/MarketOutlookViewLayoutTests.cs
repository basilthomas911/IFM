using FluentAssertions;
using System.Windows.Forms;
using TomasAI.IFM.UI.Net.Views.App;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class MarketOutlookViewLayoutTests
{
    [Fact]
    public async Task MarketDataValueRowsAreEqualAndNotClipped()
    {
        var completion = new TaskCompletionSource<LayoutSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var parent = new Panel { Width = 527, Height = 300 };
                using var view = new MarketOutlookView { Dock = DockStyle.Fill };
                parent.Controls.Add(view);
                parent.CreateControl();
                view.CreateControl();

                view.ResizeView(parent);
                parent.PerformLayout();
                view.PerformLayout();

                var marketData = FindControl<TableLayoutPanel>(view, "tlpMarketData");
                var marketTrendData = FindControl<TableLayoutPanel>(view, "tlpMarketTrendData");
                marketData.PerformLayout();
                marketTrendData.PerformLayout();

                var rowHeights = marketData.GetRowHeights();
                var trendRowHeights = marketTrendData.GetRowHeights();

                completion.SetResult(new LayoutSnapshot(
                    rowHeights,
                    CaptureValueBounds(marketData, rowHeights),
                    marketData.Controls.OfType<TextBox>()
                        .Select(control => control.Margin.Vertical)
                        .ToArray(),
                    trendRowHeights,
                    CaptureValueBounds(marketTrendData, trendRowHeights),
                    parent.Height - marketTrendData.Bottom));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var snapshot = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        thread.Join(TimeSpan.FromSeconds(10)).Should().BeTrue();

        snapshot.RowHeights.Distinct().Should().ContainSingle();
        snapshot.ValueVerticalMargins.Should().OnlyContain(margin => margin == 4,
            "2-pixel top and bottom margins make every equal row exactly 2 pixels shorter");
        snapshot.ValueBounds.Should().OnlyContain(
            value => value.ControlBottomWithMargin <= value.CellBottom,
            "every value control, including the final ITI/MDI/RSI row, must fit inside its row");
        snapshot.TrendRowHeights.Distinct().Should().ContainSingle();
        snapshot.TrendValueBounds.Should().OnlyContain(
            value => value.ControlBottomWithMargin < value.CellBottom,
            "the five bottom value controls need a visible pixel below their lower borders");
        snapshot.BottomClearance.Should().BeGreaterThanOrEqualTo(12);
    }

    [Theory]
    [InlineData(1.00f)]
    [InlineData(1.25f)]
    [InlineData(1.50f)]
    public async Task TdiRow_IsOrderedAccessibleEqualWidthAndUnclippedAtSupportedScaling(float scaleFactor)
    {
        var completion = new TaskCompletionSource<TdiLayoutSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var initialDefaultFontSize = new Label().Font.Size;
                using var parent = new Panel
                {
                    Width = (int)Math.Round(527 * scaleFactor),
                    Height = (int)Math.Round(300 * scaleFactor)
                };
                using var view = new MarketOutlookView { Dock = DockStyle.Fill };
                parent.Controls.Add(view);
                parent.CreateControl();
                view.CreateControl();
                if (scaleFactor != 1f)
                    view.Scale(new SizeF(scaleFactor, scaleFactor));

                view.ResizeView(parent);
                parent.PerformLayout();
                view.PerformLayout();

                var outlook = FindControl<TableLayoutPanel>(view, "tlpMarketOutlook");
                var marketData = FindControl<TableLayoutPanel>(view, "tlpMarketData");
                var tdi = FindControl<TableLayoutPanel>(view, "tlpTdiData");
                var trend = FindControl<TableLayoutPanel>(view, "tlpMarketTrendData");
                tdi.PerformLayout();
                var values = tdi.Controls.OfType<TextBox>().OrderBy(tdi.GetColumn).ToArray();
                var labels = tdi.Controls.OfType<Label>().OrderBy(tdi.GetColumn).ToArray();
                var columns = tdi.GetColumnWidths();
                using var rendered = new Bitmap(view.Width, view.Height);
                view.DrawToBitmap(rendered, new Rectangle(Point.Empty, rendered.Size));
                var sampledPixels = Enumerable.Range(0, Math.Max(1, rendered.Width / 4))
                    .SelectMany(x => Enumerable.Range(0, Math.Max(1, rendered.Height / 4))
                        .Select(y => rendered.GetPixel(
                            Math.Min(x * 4, rendered.Width - 1),
                            Math.Min(y * 4, rendered.Height - 1))))
                    .ToArray();

                completion.SetResult(new TdiLayoutSnapshot(
                    outlook.Top,
                    outlook.Bottom,
                    marketData.Top,
                    marketData.Bottom,
                    tdi.Top,
                    tdi.Bottom,
                    trend.Top,
                    columns,
                    values.Select(value => value.Right + value.Margin.Right <= tdi.ClientSize.Width).ToArray(),
                    values.Select(value => value.Bottom + value.Margin.Bottom <= tdi.ClientSize.Height).ToArray(),
                    values.Select(value => value.AccessibleName).ToArray(),
                    values.Select(value => value.AccessibleDescription).ToArray(),
                    labels.Zip(values, (label, value) =>
                        Math.Abs((label.Left + (label.Width / 2)) - (value.Left + (value.Width / 2)))).ToArray(),
                    sampledPixels.Any(color => color.R < 60 && color.G < 60 && color.B < 60),
                    sampledPixels.Any(color => color.R > 180 && color.G > 180 && color.B > 180),
                    initialDefaultFontSize,
                    new Label().Font.Size));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var snapshot = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        thread.Join(TimeSpan.FromSeconds(10)).Should().BeTrue();

        snapshot.OutlookBottom.Should().BeLessThanOrEqualTo(snapshot.MarketDataTop);
        snapshot.MarketDataBottom.Should().BeLessThanOrEqualTo(snapshot.TdiTop);
        snapshot.TdiBottom.Should().BeLessThanOrEqualTo(snapshot.TrendTop);
        snapshot.ColumnWidths.Max().Should().BeLessThanOrEqualTo(
            snapshot.ColumnWidths.Min() + (int)Math.Ceiling(3 * scaleFactor),
            "20-percent WinForms columns may distribute a few physical rounding pixels at scaled DPI");
        snapshot.HorizontalFit.Should().OnlyContain(fits => fits);
        snapshot.VerticalFit.Should().OnlyContain(fits => fits);
        snapshot.AccessibleNames.Should().OnlyContain(name => !string.IsNullOrWhiteSpace(name));
        snapshot.AccessibleDescriptions.Should().OnlyContain(description => !string.IsNullOrWhiteSpace(description));
        snapshot.HeaderCenterOffsets.Should().OnlyContain(offset => offset <= 1,
            "each TDI label must be centered directly above its value control");
        snapshot.HasDarkPixels.Should().BeTrue();
        snapshot.HasLightPixels.Should().BeTrue(
            "the rendered bitmap must contain visible contrasting labels and values");
        snapshot.DefaultFontAfter.Should().Be(snapshot.DefaultFontBefore,
            "the one-point font reduction must remain local to MarketOutlookView controls");
    }

    static ValueCellBounds[] CaptureValueBounds(TableLayoutPanel table, int[] rowHeights)
        => table.Controls
            .OfType<TextBox>()
            .Select(control =>
            {
                var row = table.GetRow(control);
                var cellBottom = rowHeights.Take(row + 1).Sum();
                return new ValueCellBounds(
                    control.Name,
                    control.Bottom + control.Margin.Bottom,
                    cellBottom);
            })
            .ToArray();

    static TControl FindControl<TControl>(Control parent, string name) where TControl : Control
        => parent.Controls.Find(name, true).OfType<TControl>().Single();

    sealed record LayoutSnapshot(
        int[] RowHeights,
        ValueCellBounds[] ValueBounds,
        int[] ValueVerticalMargins,
        int[] TrendRowHeights,
        ValueCellBounds[] TrendValueBounds,
        int BottomClearance);

    sealed record ValueCellBounds(
        string Name,
        int ControlBottomWithMargin,
        int CellBottom);

    sealed record TdiLayoutSnapshot(
        int OutlookTop,
        int OutlookBottom,
        int MarketDataTop,
        int MarketDataBottom,
        int TdiTop,
        int TdiBottom,
        int TrendTop,
        int[] ColumnWidths,
        bool[] HorizontalFit,
        bool[] VerticalFit,
        string?[] AccessibleNames,
        string?[] AccessibleDescriptions,
        int[] HeaderCenterOffsets,
        bool HasDarkPixels,
        bool HasLightPixels,
        float DefaultFontBefore,
        float DefaultFontAfter);
}
