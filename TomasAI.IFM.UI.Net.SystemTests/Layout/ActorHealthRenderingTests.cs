using System.Drawing;
using System.Windows.Forms;
using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Views.App;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class ActorHealthRenderingTests
{
    [Fact]
    public async Task ActorHealth_RendersDomainActorMailboxTreeAndReadOnlyDetails()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new ActorHealthForm(new Query());
                form.ShowInTaskbar = false;
                form.Opacity = 0;
                form.Show();
                System.Windows.Forms.Application.DoEvents();
                form.RefreshAsync().GetAwaiter().GetResult();
                var tree = Assert.IsType<TreeView>(Assert.Single(form.Controls.Find("actorHealthTree", true)));
                var details = Assert.IsType<DataGridView>(Assert.Single(form.Controls.Find("actorHealthDetails", true)));
                Assert.True(details.ReadOnly);
                Assert.Contains(tree.Nodes.Cast<TreeNode>(), node => node.Text == "TomasAI.IFM.Domain.Trade");
                var domain = tree.Nodes.Cast<TreeNode>().Single(node => node.Text == "TomasAI.IFM.Domain.Trade");
                Assert.Single(domain.Nodes);
                domain.Nodes[0].Expand();
                tree.SelectedNode = domain.Nodes[0];
                System.Windows.Forms.Application.DoEvents();
                Assert.Single(details.Rows.Cast<DataGridViewRow>());
                Assert.Contains("Actor Health", form.Text);
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                completed.SetResult();
            }
            catch (Exception exception) { completed.SetException(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(thread.Join(TimeSpan.FromSeconds(2)));
    }

    sealed class Query : IActorHealthQueryService
    {
        public Task<UiOperationResult<ActorHealthSnapshot>> GetAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken = default)
            => Task.FromResult(UiOperationResult<ActorHealthSnapshot>.Success(new()
            {
                ObservedUtc = DateTime.UtcNow,
                OverallStatus = 1,
                ActorCount = 1,
                RunningActorCount = 1,
                ProcessingMailboxCount = 1,
                QueuedMessageCount = 2,
                Actors = [new()
                {
                    ActorId = new() { ActorType = 2, Name = "OrderProjector" },
                    Domain = "TomasAI.IFM.Domain.Trade",
                    Implementation = "OrderProjector",
                    IsRunning = true,
                    Status = 1,
                    LifecycleState = 2,
                    Generation = 1,
                    QueueDepth = 2,
                    Mailboxes = [new()
                    {
                        ThreadId = new() { ActorType = 2, Name = "OrderProjector", EntityId = "fund-1" },
                        QueueDepth = 2,
                        IsAdmissionOpen = true,
                        Generation = 1,
                        IsProcessing = true,
                        CurrentVerb = "Project"
                    }]
                }]
            }));
    }
}
