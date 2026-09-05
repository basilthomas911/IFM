using System.Drawing;
using System.Windows.Forms;
using TomasAI.IFM.UI.Net.Models.MarketData;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Views.App;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class MarketDataOperationsHealthRenderingTests
{
    [Fact]
    public async Task Readonly_dashboard_renders_workers_metrics_and_failure_without_application_startup()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new MarketDataOperationsHealthForm(new Query());
                form.ShowInTaskbar = false;
                form.Opacity = 0;
                form.Show();
                System.Windows.Forms.Application.DoEvents();
                form.RefreshAsync().GetAwaiter().GetResult();
                form.PerformLayout();
                var stages = Assert.IsType<DataGridView>(Assert.Single(form.Controls.Find("operationsStageGrid", true)));
                var datasets = Assert.IsType<DataGridView>(Assert.Single(form.Controls.Find("operationsDatasetGrid", true)));
                Assert.True(stages.ReadOnly);
                Assert.True(datasets.ReadOnly);
                Assert.False(stages.AllowUserToAddRows);
                Assert.NotNull(stages.DataSource);
                Assert.NotNull(datasets.DataSource);
                Assert.Equal(1, stages.Rows.Count);
                var summary = Assert.IsType<Label>(Assert.Single(form.Controls.Find("operationsHealthSummary", true)));
                Assert.Contains("Red", summary.Text);
                Assert.Contains("read-only", form.Text);
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                var directory = Environment.GetEnvironmentVariable("IFM_STAGE3_RENDER_DIRECTORY");
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                    bitmap.Save(Path.Combine(directory, "operations-health.png"));
                }
                completed.SetResult();
            }
            catch (Exception exception) { completed.SetException(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(thread.Join(TimeSpan.FromSeconds(2)));
    }

    sealed class Query : IMarketDataOperationsHealthQueryService
    {
        public Task<UiOperationResult<MarketDataOperationsHealthSnapshot>> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(UiOperationResult<MarketDataOperationsHealthSnapshot>.Success(new()
            {
                ObservedOnUtc = DateTime.UtcNow, OverallStatus = "Red", SessionState = "LiveTrading", ValueDate = new(2026, 9, 4),
                Stages = [new() { Stage = "MarketOutlookComposition", Status = "Red", Pending = 12,
                    ReasonCode = "PendingWorkAged", Reason = "Injected processing stall; source data remains fresh." }],
                Datasets = [new() { Dataset = "GLBX.MDP3", Status = "Green", ProcessId = 1234,
                    WorkerInstanceId = Guid.NewGuid(), GenerationId = Guid.NewGuid(), Running = true, Healthy = true,
                    SessionState = "LiveTrading", Reason = "Synthetic worker is making progress." }]
            }));
    }
}
