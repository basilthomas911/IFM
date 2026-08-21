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
}
