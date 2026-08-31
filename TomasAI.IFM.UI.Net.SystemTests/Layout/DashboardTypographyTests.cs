using FluentAssertions;
using System.Windows.Forms;
using TomasAI.IFM.UI.Net.Views.App;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class DashboardTypographyTests
{
    [Fact]
    public async Task DashboardViewsUseTheSameFontFamilyAndPointSize()
    {
        var completion = new TaskCompletionSource<DashboardFontSnapshot[]>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var marketOutlook = new MarketOutlookView();
                using var economicCalendar = new MarketEconomicCalendarView();
                using var statusConsole = new StatusConsoleView();
                using var marketData = new MarketDataView();
                using var operations = new OperationsView();
                var views = new Control[]
                {
                    marketOutlook,
                    economicCalendar,
                    statusConsole,
                    marketData,
                    operations
                };
                completion.SetResult(views
                    .Select(view => new DashboardFontSnapshot(
                        view.Name,
                        ControlsAndSelf(view).Select(control => control.Font.Name).ToArray(),
                        ControlsAndSelf(view).Select(control => control.Font.Size).ToArray()))
                    .ToArray());
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var snapshots = await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
        thread.Join(TimeSpan.FromSeconds(30)).Should().BeTrue();

        snapshots.Should().HaveCount(5);
        snapshots.SelectMany(snapshot => snapshot.FontFamilies)
            .Should().OnlyContain(family => family == "Microsoft Sans Serif");
        snapshots.SelectMany(snapshot => snapshot.FontSizes)
            .Should().OnlyContain(size => Math.Abs(size - 8F) < 0.01F);
    }

    static IEnumerable<Control> ControlsAndSelf(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
        {
            foreach (var descendant in ControlsAndSelf(child))
                yield return descendant;
        }
    }

    sealed record DashboardFontSnapshot(
        string ViewName,
        string[] FontFamilies,
        float[] FontSizes);
}
