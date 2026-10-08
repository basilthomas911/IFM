using System.Windows.Forms;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
using TomasAI.IFM.UI.Net.Services.Trade;
using TomasAI.IFM.UI.Net.Views.Trade;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class OrderFillsNotificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Notifications_update_fills_details_colors_and_working_actions(bool filled)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var app = Substitute.For<IAppRoot>();
                var services = Substitute.For<IUiServiceCatalog>();
                app.Services.Returns(services);
                var notifications = Substitute.For<IOrderExecutionNotificationService>();
                services.OrderExecutionNotifications.Returns(notifications);
                var subscription = Substitute.For<IUiEventSubscription>();
                Action<OrderExecutionChangedEvent>? onExecution = null;
                Action<BrokerOrderChangedEvent>? onBroker = null;
                notifications.CreateSubscription(Arg.Do<Action<OrderExecutionChangedEvent>>(x => onExecution = x),
                    Arg.Do<Action<BrokerOrderChangedEvent>>(x => onBroker = x)).Returns(subscription);
                var id = new TradeOrderId(1, 4, 10);
                var executionId = new OrderExecutionId(id, Guid.NewGuid());
                var componentId = Guid.NewGuid();
                var definition = new TradeOrderDefinition { Id = id, Components = [new()
                {
                    ComponentId = componentId, TickIncrement = .05m, SignedNetDebitLimit = -2m,
                    MinimumSignedNetDebitLimit = -2.5m, MaximumSignedNetDebitLimit = -2m
                }] };
                var broker = new BrokerOrderDefinition { Id = new(executionId, componentId), Order = definition,
                    Status = BrokerOrderStatus.Working, Revision = 1, CurrentSignedNetDebitLimit = -2m };
                var execution = new OrderExecutionDefinition { TradeOrderId = id, ExecutionAttemptId = executionId.ExecutionAttemptId,
                    Order = definition, OrderQuantity = 2, PositionType = TradeOrderPositionType.Opening, Status = OrderExecutionStatus.Submitted };
                services.BrokerOrders.ListAsync(id, Arg.Any<CancellationToken>()).Returns(new ServiceOk<BrokerOrderDefinition[]>([broker]));
                services.OrderExecutions.GetAsync(id, executionId.ExecutionAttemptId, Arg.Any<CancellationToken>()).Returns(new ServiceOk<OrderExecutionDefinition>(execution));
                var fund = new PortfolioFundEditorModel(4, "Fund", "", 0, false, DateTime.UtcNow, "test");
                var order = new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { PortfolioId = 1, FundId = 4, OrderId = 10 });
                var trade = new PortfolioFundOrderTradeEditorModel { FundId = 4, OrderId = 10, TradeId = 1, TradeState = TradeState.NewTrade, TradeType = TradeType.ShortIronCondor };
                using var view = new OrderFillsPreviewControl(1, fund, order, trade, appRoot: app);
                var openingFillReports = 0;
                view.OpeningOrdersFilled += () => { openingFillReports++; return Task.CompletedTask; };
                using var form = new Form { Width = 1200, Height = 650, ShowInTaskbar = false, Location = new(-3000, -3000) };
                form.Controls.Add(view); form.Show();
                view.BindSubmittedOrders([definition]);
                System.Windows.Forms.Application.DoEvents();
                var tree = (TreeView)view.Controls.Find("brokerPreviewOrderTree", true).Single();
                var detail = (PropertyGrid)view.Controls.Find("brokerPreviewSelectedDetail", true).Single();
                var status = (Label)view.Controls.Find("orderExecutionNotificationStatus", true).Single();
                var update = view.Controls.Find("brokerPreviewUpdateLimit", true).Single();
                var price = view.Controls.Find("brokerPreviewUpdateOrderPrice", true).Single();
                var cancel = view.Controls.Find("brokerPreviewCancelUnfilled", true).Single();
                Assert.True(update.Enabled && price.Enabled && cancel.Enabled);
                Assert.Equal(Color.Yellow, tree.Nodes[0].ForeColor);
                var fill = new ExecutionFillEvidence { ExecutionFillId = Guid.NewGuid(), ComponentId = componentId, ContractId = "ES", SignedQuantity = 1, Price = 100m };
                var partial = execution with { Status = OrderExecutionStatus.PartiallyFilled, CumulativeFilledQuantity = 1, Fills = [fill] };
                var notification = new OrderExecutionChangedEvent { EntityId = executionId, EventId = 2, ReceivedOn = DateTime.UtcNow,
                    Subject = new(ActorType.Event, OrderExecutionChangedEvent.Actor, OrderExecutionChangedEvent.Verb, executionId.Format()), OrderExecutionDefinition = partial };
                Task.Run(() => onExecution!(notification)).GetAwaiter().GetResult();
                System.Windows.Forms.Application.DoEvents();
                Assert.Single(tree.Nodes[0].Nodes);
                Assert.Contains("1/2", status.Text);
                Assert.Contains("OrderExecutionChanged #2", status.Text);
                tree.SelectedNode = tree.Nodes[0].Nodes[0];
                Assert.Equal(fill, detail.SelectedObject);
                Assert.True(update.Enabled && price.Enabled && cancel.Enabled);
                view.RefreshAsync().GetAwaiter().GetResult();
                Assert.Single(tree.Nodes[0].Nodes); // Stale query must not erase a notification's fill.
                var final = partial with { Status = filled ? OrderExecutionStatus.Filled : OrderExecutionStatus.Cancelled,
                    CumulativeFilledQuantity = filled ? 2 : 1, Fills = [fill with { SignedQuantity = filled ? 2 : 1 }] };
                onExecution!(notification with { EventId = 3, ReceivedOn = DateTime.UtcNow.AddSeconds(1), OrderExecutionDefinition = final });
                System.Windows.Forms.Application.DoEvents();
                Assert.StartsWith(filled ? "Filled" : "Cancelled", status.Text);
                Assert.Equal(filled ? Color.LimeGreen : Color.Red, status.ForeColor);
                Assert.Equal(status.ForeColor, tree.Nodes[0].ForeColor);
                // Verify the custom painter uses the status color, including for a selected node.
                using (var bitmap = new Bitmap(1000, 40))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Black);
                    var draw = typeof(BrokerOrderFillsPreviewControl).GetMethod("DrawTreeNode",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                    draw.Invoke(view, [tree, new DrawTreeNodeEventArgs(graphics, tree.Nodes[0],
                        new Rectangle(0, 0, 800, 30), TreeNodeStates.Selected)]);
                    Assert.Contains(Enumerable.Range(0, bitmap.Width), x =>
                        Enumerable.Range(0, bitmap.Height).Any(y => bitmap.GetPixel(x, y).ToArgb() == status.ForeColor.ToArgb()));
                    Assert.DoesNotContain(Enumerable.Range(0, bitmap.Width), x =>
                        Enumerable.Range(0, bitmap.Height).Any(y => bitmap.GetPixel(x, y).ToArgb() == Color.Yellow.ToArgb()));
                }

                Assert.Equal(filled ? 1 : 0, openingFillReports);
                Assert.False(update.Enabled || price.Enabled || cancel.Enabled);
                Assert.Equal(final.Fills[0], detail.SelectedObject);
                onExecution!(notification); // Late delivery cannot revive working controls.
                onBroker!(new BrokerOrderChangedEvent { EntityId = broker.Id, BrokerOrderDefinition = broker, ReceivedOn = DateTime.UtcNow });
                System.Windows.Forms.Application.DoEvents();
                Assert.False(update.Enabled || price.Enabled || cancel.Enabled);
                Assert.Equal(filled ? Color.LimeGreen : Color.Red, status.ForeColor);
                Assert.Equal(filled ? 1 : 0, openingFillReports);
                view.Dispose();
                subscription.Received(1).DisposeAsync();
                done.SetResult();
            }
            catch (Exception error) { done.SetException(error); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
}
