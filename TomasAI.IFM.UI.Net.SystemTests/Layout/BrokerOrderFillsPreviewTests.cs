using System.Windows.Forms;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Views.Trade;
using ComboBox = System.Windows.Forms.ComboBox;
using DataGridView = System.Windows.Forms.DataGridView;
using DataGridViewRow = System.Windows.Forms.DataGridViewRow;
using Label = System.Windows.Forms.Label;
using TextBox = System.Windows.Forms.TextBox;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class BrokerOrderFillsPreviewTests
{
    [Fact]
    public async Task Iron_condor_preview_supports_separate_broker_trade_and_order_fills_tabs_without_actions()
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
        })
        { IsBackground = true };
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
            PortfolioId = 1,
            FundId = 4,
            OrderId = 16001
        });
        var trade = new PortfolioFundOrderTradeEditorModel
        {
            PortfolioId = 1,
            FundId = 4,
            OrderId = 16001,
            TradeId = 2,
            TradeType = TradeType.ShortIronCondor,
            BaseContractId = "ES20261218",
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
        var limit = (TextBox)preview.Controls.Find("brokerPreviewNetLimitTicks", true).Single();
        Assert.Equal("16.00", limit.Text);
        Assert.True(limit.ReadOnly);
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

        using var brokerTrade = new BrokerTradePreviewControl(1, fund, order, trade);
        using var orderFills = new OrderFillsPreviewControl(1, fund, order, trade);
        host.Controls.Clear();
        host.ClientSize = new Size(1200, 900);
        using var splitTabs = new TabControl { Dock = DockStyle.Fill };
        splitTabs.TabPages.Add(new TabPage("Broker Trade"));
        splitTabs.TabPages[0].Controls.Add(brokerTrade);
        splitTabs.TabPages.Add(new TabPage("Order Fills"));
        splitTabs.TabPages[1].Controls.Add(orderFills);
        host.Controls.Add(splitTabs);
        System.Windows.Forms.Application.DoEvents();

        using var automation = new UIA3Automation();
        var renderedWindow = automation.FromHandle(host.Handle).AsWindow();
        var brokerViewport = renderedWindow.FindFirstDescendant(
            condition => condition.ByAutomationId("brokerTradePreview"));
        Assert.NotNull(brokerViewport);
        foreach (var row in Descendants(brokerTrade).OfType<Panel>()
                     .Where(panel => panel.AccessibleName == "Broker Trade field row"))
        {
            var captions = row.Controls.Cast<Control>()
                .Where(control => control.Name.EndsWith("Label", StringComparison.Ordinal)).ToArray();
            var editors = row.Controls.Cast<Control>()
                .Where(control => !control.Name.EndsWith("Label", StringComparison.Ordinal)).ToArray();
            Assert.Equal(captions.Length, editors.Length);
            for (var index = 0; index < captions.Length; index++)
            {
                var caption = captions[index];
                var editor = editors[index];
            Assert.False(string.IsNullOrWhiteSpace(caption.Name));
            Assert.False(string.IsNullOrWhiteSpace(editor.Name));
            var renderedCaption = renderedWindow.FindFirstDescendant(
                condition => condition.ByAutomationId(caption.Name));
            var renderedEditor = renderedWindow.FindFirstDescendant(
                condition => condition.ByAutomationId(editor.Name));
            Assert.NotNull(renderedCaption);
            Assert.NotNull(renderedEditor);
            Assert.False(renderedCaption.IsOffscreen);
            Assert.False(renderedEditor.IsOffscreen);
            var directRowHeight = caption.Height;
            Assert.True(renderedCaption.BoundingRectangle.Height >= directRowHeight - 1,
                $"{caption.Name} is clipped: {renderedCaption.BoundingRectangle}");
            Assert.True(renderedEditor.BoundingRectangle.Height >= 20,
                $"{editor.Name} is clipped: {renderedEditor.BoundingRectangle}");
            Assert.True(renderedEditor.BoundingRectangle.Top >=
                renderedCaption.BoundingRectangle.Bottom + 2,
                $"{row.Name} label/editor overlap: {renderedCaption.BoundingRectangle} / {renderedEditor.BoundingRectangle}");
            Assert.True(brokerViewport.BoundingRectangle.Contains(renderedCaption.BoundingRectangle),
                $"{caption.Name} {renderedCaption.BoundingRectangle} is outside Broker Trade {brokerViewport.BoundingRectangle}.");
            Assert.True(brokerViewport.BoundingRectangle.Contains(renderedEditor.BoundingRectangle),
                $"{editor.Name} {renderedEditor.BoundingRectangle} is outside Broker Trade {brokerViewport.BoundingRectangle}.");
            }
        }

        Assert.Equal(new[] { "Broker Trade", "Order Fills" },
            splitTabs.TabPages.Cast<TabPage>().Select(page => page.Text));
        var brokerTradeSplit = (SplitContainer)brokerTrade.Controls
            .Find("brokerOrderFillsVerticalSplit", true).Single();
        var orderFillsSplit = (SplitContainer)orderFills.Controls
            .Find("brokerOrderFillsVerticalSplit", true).Single();
        Assert.True(brokerTradeSplit.Panel2Collapsed);
        Assert.True(orderFillsSplit.Panel1Collapsed);
        Assert.True(brokerTradeSplit.Panel1.Height > 0);
        Assert.True(orderFillsSplit.Panel2.Height > 0);
        Assert.Equal(4, brokerTrade.Controls.Find("brokerPreviewLegGrid", true)
            .Cast<DataGridView>().Single().RowCount);
        Assert.Equal(3, orderFills.Controls.Find("brokerPreviewOrderTree", true)
            .Cast<TreeView>().Single().Nodes.Count);
        var brokerOrderPane = (Panel)brokerTrade.Controls
            .Find("brokerPreviewOrderPane", true).Single();
        var brokerOrderContent = (TableLayoutPanel)brokerTrade.Controls
            .Find("brokerPreviewOrderContent", true).Single();
        Assert.False(brokerOrderPane.AutoScroll);
        Assert.True(brokerOrderContent.Bottom <= brokerOrderPane.ClientSize.Height,
            $"Broker Order content bottom {brokerOrderContent.Bottom} exceeds visible pane height {brokerOrderPane.ClientSize.Height}.");
        var brokerRows = Descendants(brokerTrade)
            .OfType<Panel>()
            .Where(panel => panel.AccessibleName == "Broker Trade field row")
            .ToArray();
        Assert.NotEmpty(brokerRows);
        Assert.All(brokerRows, row =>
        {
            var captions = row.Controls.Cast<Control>()
                .Where(control => control.Name.EndsWith("Label", StringComparison.Ordinal)).ToArray();
            var editors = row.Controls.Cast<Control>()
                .Where(control => !control.Name.EndsWith("Label", StringComparison.Ordinal)).ToArray();
            Assert.Equal(captions.Length, editors.Length);
            Assert.All(captions, caption => Assert.Equal(24, caption.Height));
            for (var index = 0; index < captions.Length; index++)
            {
                Assert.Equal(captions[index].Height, editors[index].Height);
                Assert.True(editors[index].Top >= captions[index].Bottom + 2,
                    $"{row.Name} has no separator between {captions[index].Name} and {editors[index].Name}.");
            }
            Assert.All(row.Controls.Cast<Control>(), control =>
                Assert.True(control.Bottom <= row.ClientSize.Height,
                    $"{row.Name}/{control.Name} extends below its row."));
        });
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }
}
