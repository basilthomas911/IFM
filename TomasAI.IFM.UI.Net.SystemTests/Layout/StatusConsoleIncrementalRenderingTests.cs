using FluentAssertions;
using System.Windows.Forms;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using TomasAI.IFM.UI.Net.Views.App;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class StatusConsoleIncrementalRenderingTests
{
    [Fact]
    public async Task Updating_an_overlapping_snapshot_prepends_only_the_new_rows()
    {
        var completion = new TaskCompletionSource<StatusConsoleRendering>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var view = new StatusConsoleView();
                var list = view.Controls.Find("lstStatusConsole", true)
                    .OfType<ListView>()
                    .Single();
                var oldest = CreateLog("oldest", 1);
                var previousNewest = CreateLog("previous-newest", 2);
                view.RenderStatusConsole([previousNewest, oldest]);
                var retainedItem = list.Items[0];

                var newer = CreateLog("newer", 3);
                var newest = CreateLog("newest", 4);
                view.UpdateStatusConsole([newest, newer, previousNewest, oldest]);

                completion.SetResult(new StatusConsoleRendering(
                    list.Items.Cast<ListViewItem>().Select(item => item.SubItems[1].Text).ToArray(),
                    ReferenceEquals(retainedItem, list.Items[2])));
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
        rendering.Messages.Should().Equal("newest", "newer", "previous-newest", "oldest");
        rendering.PreviousItemWasRetained.Should().BeTrue();
    }

    static StatusConsoleLogReadModel CreateLog(string message, int sequence)
        => new(
            new DateTime(2026, 1, 1, 0, 0, sequence, DateTimeKind.Utc),
            (int)StatusCodeType.Ok,
            LogSourceType.TestSource,
            message);

    sealed record StatusConsoleRendering(string[] Messages, bool PreviousItemWasRetained);
}
