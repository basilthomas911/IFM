using System.Windows.Forms;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Views.Trade;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class BrokerOrderFillsPreviewTests
{
    [Fact]
    public async Task Iron_condor_preview_shows_four_legs_and_date_wide_order_fill_tree_without_actions()
    {
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            using var context = new ApplicationContext();
            using var dispatcher = new Control();
            _ = dispatcher.Handle;
            dispatcher.BeginInvoke((Action)(() =>
            {
                try
                {
                    VerifyPreview();
                    completed.SetResult();
                }
                catch (Exception error) { completed.SetException(error); }
                finally { context.ExitThread(); }
            }));
            System.Windows.Forms.Application.Run(context);
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }

    private static void VerifyPreview()
    {
        var fund = new PortfolioFundEditorModel(4, "Preview Fund", "", 0, false,
            DateTime.UtcNow, "test");
        var order = new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel
        {
            PortfolioId = 1, FundId = 4, OrderId = 16001
        });
        var trade = new PortfolioFundOrderTradeEditorModel
        {
            PortfolioId = 1, FundId = 4, OrderId = 16001, TradeId = 2,
            TradeType = TradeType.ShortIronCondor, BaseContractId = "ES20261218",
            RequestedTradeDate = new DateOnly(2026, 9, 23)
        };
        using var preview = new BrokerOrderFillsPreviewControl(1, fund, order, trade);
        using var host = new Form { Width = 1400, Height = 900, ShowInTaskbar = false };
        host.Controls.Add(preview);
        host.Show();
        System.Windows.Forms.Application.DoEvents();
        var screenshotPath = Environment.GetEnvironmentVariable("IFM_BROKER_PREVIEW_SCREENSHOT");
        if (!string.IsNullOrWhiteSpace(screenshotPath))
        {
            using var bitmap = new Bitmap(preview.Width, preview.Height);
            preview.DrawToBitmap(bitmap, new Rectangle(Point.Empty, preview.Size));
            bitmap.Save(screenshotPath);
        }
        Assert.Contains("Fund Order 16001",
            preview.Controls.Find("brokerPreviewIdentity", true).Single().Text);
        var legs = (DataGridView)preview.Controls.Find("brokerPreviewLegGrid", true).Single();
        Assert.Equal(4, legs.RowCount);
        Assert.Equal(new[] { "+LP", "−SP", "−SC", "+LC" },
            legs.Rows.Cast<DataGridViewRow>().Select(row => row.Cells["Role"].Value?.ToString()));
        var algorithm = (ComboBox)preview.Controls.Find("brokerPreviewIFMalgorithmSelector", true).Single();
        var pace = (ComboBox)preview.Controls.Find("brokerPreviewPaceSelector", true).Single();
        Assert.Equal("None", algorithm.SelectedItem);
        Assert.False(pace.Enabled);
        algorithm.SelectedItem = "IFM Atomic Combo";
        Assert.True(pace.Enabled);
        Assert.Equal("Normal", pace.SelectedItem);
        var limit = (NumericUpDown)preview.Controls.Find("brokerPreviewNetLimitTicks", true).Single();
        Assert.Equal(0.25m, limit.Increment);
        Assert.All(preview.Controls.Find("brokerPreviewPlaceOrder", true),
            control => Assert.False(control.Enabled));
        Assert.All(preview.Controls.Find("brokerPreviewUpdateLimit", true),
            control => Assert.False(control.Enabled));
        Assert.All(preview.Controls.Find("brokerPreviewCancelUnfilled", true),
            control => Assert.False(control.Enabled));
        var tree = (TreeView)preview.Controls.Find("brokerPreviewOrderTree", true).Single();
        Assert.Equal(3, tree.Nodes.Count);
        Assert.Equal(2, tree.Nodes[0].Nodes.Count);
        tree.SelectedNode = tree.Nodes[0].Nodes[0];
        var detail = (Label)preview.Controls.Find("brokerPreviewSelectedDetail", true).Single();
        Assert.Contains("Fill 001", detail.Text);
        Assert.Contains("Order mutation is unavailable", detail.Text);
        var upper = (SplitContainer)preview.Controls.Find("brokerOrderFillsVerticalSplit", true).Single();
        Assert.True(upper.Panel1.Height > 0 && upper.Panel2.Height > 0);
        host.Size = new Size(960, 500);
        System.Windows.Forms.Application.DoEvents();
        Assert.True(upper.Panel1.Height > 0 && upper.Panel2.Height > 0);
        Assert.Equal(4, legs.RowCount);
    }
}
