using FluentAssertions;
using NSubstitute;
using System.Reflection;
using System.Windows.Forms;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.ViewModels.Trade.IronCondor;
using TomasAI.IFM.UI.Net.Views.Trade.IronCondor;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

public sealed class IronCondorTradePlanVirtualListTests
{
    [Fact]
    public async Task Virtual_history_keeps_all_rows_and_replaces_the_selected_date()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                var services = Substitute.For<IUiServiceCatalog>();
                services.CommandResponses.Returns(new TomasAI.IFM.UI.Net.Services.Application.CommandResponseEventService(
                    Substitute.For<TomasAI.IFM.UI.EventConsumer.ICommandResponseUIEventConsumer>()));
                var root = Substitute.For<IAppRoot>(); root.Services.Returns(services);
                var trade = new EstablishedTradeDefinition { Id = new(101,701,1701,1101), StrategyKind = TradeStrategyKind.IronCondor,
                    Legs = Enumerable.Range(0,4).Select(i => new TradeLegDefinition {
                        TradeLegId = Guid.NewGuid(), ContractId = $"saved-{i}", PutCall = (byte)(i < 2 ? 1 : 2),
                        SignedQuantity = i % 2 == 0 ? 1 : -1, CashMultiplier = 50
                    }).ToArray() };
                var vm = new IronCondorViewModel(root,
                    new PortfolioFundEditorModel(701,"Fund","",0,false,DateTime.UtcNow,"test"),
                    new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { OrderId = 1701 }),
                    new PortfolioFundOrderTradeEditorModel { TradeId = 1101 }, null, [], establishedTrade: trade);
                using var parent = new Panel();
                using var view = new IronCondorTradeView(parent, vm);
                var savedPosition = new StrategyPositionSnapshot {
                    Legs = trade.Legs.Select(leg => new StrategyPositionLeg {
                        TradeLegId = leg.TradeLegId, ContractId = leg.ContractId, SignedQuantity = leg.SignedQuantity,
                        CurrentPrice = leg.SignedQuantity > 0 ? 2m : 5m, OpeningPrice = leg.SignedQuantity > 0 ? 1m : 5m
                    }).ToArray()
                };
                typeof(IronCondorViewModel).GetField("_selectedSavedPosition",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(vm,savedPosition);
                typeof(IronCondorTradeView).GetMethod("RenderSavedPositionData",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(view,null);
                view.Controls.Find("txtPutNetSpread",true).Single().Text.Should().Be((-3m).ToString("0.00"));
                view.Controls.Find("txtCallTradeValue",true).Single().Text.Should().Be((-150m).ToString("C"));
                view.Controls.Find("txtPutTradePnl",true).Single().Text.Should().Be(50m.ToString("C"));
                view.Controls.Find("txtCallShortDelta",true).Single().Text.Should().Be("N/A");
                var limits = new TomasAI.IFM.Domain.Trade.Shared.ViewModels.TradeLimitReadModel {
                    TradeId = 1101, RiskMargin = 1847.50m, MaxProfit = 652.50m
                };
                var showLimits = typeof(IronCondorTradeView).GetMethod("ShowTradeLimits", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var limitsList = (ListView)typeof(IronCondorTradeView).GetField("lstTradeLimit", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
                showLimits.Invoke(view, [1701, (limits, (decimal?)null)]);
                showLimits.Invoke(view, [1701, (limits, (decimal?)null)]);
                limitsList.Items.Count.Should().Be(1);
                limitsList.Items[0].SubItems[2].Text.Should().Be(limits.MaxProfit.ToString("C"));
                limitsList.Items[0].SubItems[9].Text.Should().Be("N/A");
                var date = new DateOnly(2026,10,6);
                var plans = Enumerable.Range(1,451).Select(i => new StrategyTradePlanSnapshot {
                    Position = new StrategyPositionSnapshot { Id = StrategyPositionId.Create(trade.Id, TradeStrategyKind.IronCondor) },
                    ValueDate = date, PlanRevision = i,
                    IronCondorTradePlanSnapshot = new IronCondorTradePlanSnapshot { SequenceId = i, DailyPnl = -i, ForwardLossRatio = .5 }
                }).ToArray();
                var historyField = typeof(IronCondorViewModel).GetField("_ironCondorPlanHistory", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var render = typeof(IronCondorTradeView).GetMethod("RenderPlanHistory", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var list = (ListView)typeof(IronCondorTradeView).GetField("lstTradePlanAction", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
                historyField.SetValue(vm, plans);
                render.Invoke(view,null);
                list.VirtualMode.Should().BeTrue();
                list.Columns.Cast<ColumnHeader>().Take(4).Select(column => column.Text).Should().Equal("ActionDateTime", "SequenceId", "DailyPnl", "ForwardLossRatio");
                list.VirtualListSize.Should().Be(451);
                list.Items[450].Tag.Should().BeSameAs(plans[450].IronCondorTradePlanSnapshot);
                historyField.SetValue(vm, plans[..3]);
                render.Invoke(view,null);
                list.VirtualListSize.Should().Be(3);
                list.Items[2].Tag.Should().BeSameAs(plans[2].IronCondorTradePlanSnapshot);
                historyField.SetValue(vm, Array.Empty<StrategyTradePlanSnapshot>());
                render.Invoke(view,null);
                list.VirtualListSize.Should().Be(0);
                completion.SetResult();
            }
            catch (Exception exception) { completion.SetException(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }
}
