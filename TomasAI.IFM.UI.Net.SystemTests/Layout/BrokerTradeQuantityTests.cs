using System.Windows.Forms;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Net.Views.Trade;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class BrokerTradeQuantityTests
{
    [Fact]
    public async Task Quantity_edits_recalculate_economics_and_survive_quote_refresh()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var fund = new PortfolioFundEditorModel(4, "Fund", "", 0, false, DateTime.UtcNow, "test");
                var order = new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { PortfolioId = 1, FundId = 4, OrderId = 1 });
                var trade = new PortfolioFundOrderTradeEditorModel { FundId = 4, OrderId = 1, TradeId = 1, TradeState = TradeState.NewTrade, TradeType = TradeType.ShortIronCondor };
                using var view = new BrokerTradePreviewControl(1, fund, order, trade);
                using var form = new Form { Width = 1300, Height = 900, ShowInTaskbar = false };
                form.Controls.Add(view);
                form.Show();
                BrokerTradeLegData[] legs = [
                    new("LP", "+LP", 95, 1, 1, -.1, false, 1, 50),
                    new("SP", "-SP", 100, 2, 2, -.2, false, 1, 50),
                    new("SC", "-SC", 110, 2, 2, .2, true, 1, 50),
                    new("LC", "+LC", 115, 1, 1, .1, true, 1, 50) ];
                Assert.Empty(view.Controls.Find("brokerPreviewPreviewOrder", true));
                var place = view.Controls.Find("brokerPreviewPlaceOrder", true).Single();
                Assert.Equal("brokerPreviewActions", place.Parent!.Name);
                Assert.Equal(DockStyle.Bottom, place.Parent.Dock);
                Assert.Equal(FlowDirection.RightToLeft, ((FlowLayoutPanel)place.Parent).FlowDirection);
                using var fills = new OrderFillsPreviewControl(1, fund, order, trade);
                var update = fills.Controls.Find("brokerPreviewUpdateLimit", true).Single();
                var cancel = fills.Controls.Find("brokerPreviewCancelUnfilled", true).Single();
                Assert.Equal("brokerPreviewFillActions", update.Parent!.Name);
                Assert.Same(update.Parent, cancel.Parent);
                Assert.Equal(DockStyle.Fill, update.Parent.Dock);
                Assert.Equal("brokerPreviewFillsLayout", update.Parent.Parent!.Name);
                var orderPrice = (NumericUpDown)fills.Controls.Find("brokerPreviewUpdateOrderPrice", true).Single();
                Assert.Equal(0.05m, orderPrice.Increment);
                orderPrice.Value = 1m;
                orderPrice.UpButton();
                Assert.Equal(1.05m, orderPrice.Value);
                orderPrice.DownButton();
                Assert.Equal(1m, orderPrice.Value);
                Assert.Equal("Update Order Price", update.Text);
                Assert.Equal(1, update.Parent.Controls.GetChildIndex(orderPrice));
                Assert.False(update.Enabled); Assert.False(cancel.Enabled);
                foreach (var state in new[] { TradeState.OrderSubmitted, TradeState.OrderPlaced, TradeState.OrderPartiallyFilled })
                {
                    fills.SetTradeState(state);
                    Assert.True(update.Enabled); Assert.True(cancel.Enabled);
                }
                foreach (var state in new[] { TradeState.NewTrade, TradeState.OrderFilled, TradeState.OrderCancelled, TradeState.OrderCompleted })
                {
                    fills.SetTradeState(state);
                    Assert.False(update.Enabled); Assert.False(cancel.Enabled);
                }
                fills.SetTradeState(TradeState.OrderPlaced, readOnly: true);
                Assert.False(update.Enabled); Assert.False(cancel.Enabled);
                view.SetOrderTypeEditable(true);
                ComboBox Selector(string caption) => (ComboBox)view.Controls.Find("brokerPreview" + caption.Replace(" ", "") + "Selector", true).Single();
                Assert.True(Selector("Time in force").Enabled);
                Assert.True(Selector("Action").Enabled);
                Assert.True(Selector("IFM algorithm").Enabled);
                Selector("Time in force").SelectedItem = "GTC";
                Selector("IFM algorithm").SelectedItem = "Adaptive";
                Assert.True(Selector("Pace").Enabled);
                Selector("Pace").SelectedItem = "Urgent";
                Assert.Equal("GTC", view.SelectedTimeInForce);
                Assert.Equal("Adaptive", view.SelectedAlgorithm);
                Assert.Equal("Urgent", view.SelectedPace);
                Assert.Equal("Open", view.SelectedAction);
                view.BindSelectedLegs(legs);
                var orderPriceInput = (TextBox)view.Controls.Find("brokerPreviewNetLimitTicks", true).Single();
                Assert.False(orderPriceInput.ReadOnly);
                orderPriceInput.Text = "-2.05";
                view.BindSelectedLegs(legs);
                Assert.Equal(-2.05m, view.SelectedOrderPrice);
                Assert.Equal(102.5m, decimal.Parse(view.Controls.Find("brokerPreviewMaxprofitValue", true).Single().Text));
                Assert.Equal(147.5m, decimal.Parse(view.Controls.Find("brokerPreviewMaxlossValue", true).Single().Text));
                orderPriceInput.Text = "-2";
                view.BindSelectedLegs(legs);
                var quantity = (NumericUpDown)view.Controls.Find("brokerPreviewContractsValue", true).Single();
                Assert.Equal(1, quantity.Value); Assert.Equal(1, quantity.Minimum);
                Assert.Equal(1, quantity.Increment); Assert.Equal(0, quantity.DecimalPlaces);
                Assert.Throws<ArgumentOutOfRangeException>(() => quantity.Value = 0);
                string Field(string name) => view.Controls.Find("brokerPreview" + name + "Value", true).Single().Text;
                Assert.Equal(100m, decimal.Parse(Field("Maxprofit")));
                Assert.Equal(150m, decimal.Parse(Field("Maxloss")));
                quantity.Value = 3;
                Assert.All(view.LegQuantities.Values, value => Assert.Equal(3, value));
                Assert.Equal(300m, decimal.Parse(Field("Maxprofit")));
                Assert.Equal(450m, decimal.Parse(Field("Maxloss")));
                view.BindSelectedLegs(legs);
                Assert.All(view.LegQuantities.Values, value => Assert.Equal(3, value));
                var legQuantity = (NumericUpDown)view.Controls.Find("brokerPreviewLegQuantity", true).Single();
                legQuantity.Value = 2;
                Assert.All(view.LegQuantities.Values, value => Assert.Equal(2, value));
                Assert.Equal(2, quantity.Value);
                Assert.DoesNotContain("Unequal", Field("Validation"));
                legQuantity.UpButton();
                Assert.All(view.LegQuantities.Values, value => Assert.Equal(3, value));
                legQuantity.DownButton();
                Assert.All(view.LegQuantities.Values, value => Assert.Equal(2, value));
                quantity.Value = 4;
                Assert.All(view.LegQuantities.Values, value => Assert.Equal(4, value));
                Assert.Contains("Fee schedule unavailable", Field("Est.fees"));
                completion.SetResult();
            }
            catch (Exception exception) { completion.SetException(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
