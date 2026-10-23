using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Services;
using TomasAI.IFM.UI.Net.Services.Trade;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Services;

public sealed class ManualOrderAcknowledgementLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Confirm_keeps_dialog_until_the_exact_broker_acceptance_or_rejection(bool rejected)
    {
        var root=Substitute.For<IAppRoot>(); var services=Substitute.For<IUiServiceCatalog>();
        root.Services.Returns(services); root.AppEnvironment.Returns("Development");
        root.DiagnosticLogger.Returns(NullLogger.Instance);
        root.TradeExecution.Returns(new DesktopTradeExecutionContext("EMU",BrokerEnvironment.Emulator));
        var portfolio=Substitute.For<IPortfolioOrderCompositionApi>(); var lifecycle=Substitute.For<ITradeOrderLifecycleApi>();
        var queries=Substitute.For<IBrokerOrderQueryApi>(); services.BrokerOrders.Returns(queries);
        services.PortfolioTradeOrders.Returns(new PortfolioTradeOrderService(portfolio,lifecycle));
        var tradeQueries=Substitute.For<ITradeQueryApi>();
        tradeQueries.PrepareTradeOrderAsync(Arg.Any<TomasAI.IFM.Domain.Trade.Shared.Order.TradeOrderPreparationRequest>(),Arg.Any<CancellationToken>())
            .Returns(call => new ServiceOk<TomasAI.IFM.Domain.Trade.Shared.Order.TradeOrderPreparation>(
                TomasAI.IFM.Domain.Trade.Order.Query.Model.TradeOrderPreparationCompute.Calculate(
                    call.Arg<TomasAI.IFM.Domain.Trade.Shared.Order.TradeOrderPreparationRequest>(),
                    new() { Symbol="ES",Currency="USD",Exchange="XCME" },new(2026,10,15),DateTime.UtcNow,.25m,.65m,default!,"Manual","test")));
        services.TradeQueries.Returns(new TradeQueryService(tradeQueries));
        Guid component=default; var observed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int responded=0;
        portfolio.EvaluateAsync(Arg.Any<EvaluatePortfolioOrderCompositionCommand>(),Arg.Any<CancellationToken>()).Returns(call=>{
            var request=call.Arg<EvaluatePortfolioOrderCompositionCommand>(); component=request.Body.Components.Single().ComponentId;
            request.Body.Components.Single().Legs.Single().ContractId.Should().Be("ES20261218");
            return new ServiceOk<FunctionResult<PortfolioOrderCompositionCompletedEvent,PortfolioOrderCompositionFailedEvent>>(
                FunctionResult<PortfolioOrderCompositionCompletedEvent,PortfolioOrderCompositionFailedEvent>.Complete(new() {
                    Id=Guid.NewGuid(),OperationId=request.OperationId,PortfolioId=1,Receipt=new() {
                        CompositionId=request.Body.CompositionId,Status=PortfolioOrderCompositionStatus.ExecuteTradeOrders,
                        TradeOrders=[new() { Id=new(1,2,300),PositionType=PortfolioExecutionPositionType.Opening,
                            Components=request.Body.Components,Status=PortfolioExecutionOrderStatus.Approved }]
                    }
                }));
        });
        lifecycle.SubmitAcceptedAsync(Arg.Any<TradeOrderDefinition>(),Arg.Any<Guid>(),ExecutionChannel.Broker,Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        queries.ListAsync(Arg.Any<TradeOrderId>(),Arg.Any<CancellationToken>()).Returns(call=>{
            observed.TrySetResult();
            return new ServiceOk<BrokerOrderDefinition[]>([new() {
                Id=new(new(call.Arg<TradeOrderId>(),Guid.NewGuid()),component),
                Status=Volatile.Read(ref responded)==0 ? BrokerOrderStatus.Dispatched : rejected ? BrokerOrderStatus.Rejected : BrokerOrderStatus.Working,
                LastObservation=new() { Category="TEST.REJECT",Detail="Broker declined this order" }
            }]);
        });
        var order=new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel { PortfolioId=1,FundId=2,OrderId=100 });
        var trade=new PortfolioFundOrderTradeEditorModel { PortfolioId=1,FundId=2,OrderId=100,TradeId=200,PrimaryTrade=true,
            TradeType=TradeType.FuturesOutright,TradeAction=TradeAction.Buy,RequestedTradeDate=new(2026,10,15) };
        var model=new BrokerManualTradeOrderViewModel(root,1,order,trade,
            new("ES20261218","ESZ6","ES","ESZ6","FUT","USD","XCME","50",new(2026,12,18),true,true));
        var dialog=new TestDialog(); var submission=model.SubmitAsync(1,10m,dialog);
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        dialog.Closed.Should().BeFalse(); model.BrokerAccepted.Should().BeFalse();
        model.RequiresSubmissionReconciliation.Should().BeTrue();
        Volatile.Write(ref responded,1);
        if(rejected) {
            var act=async()=>await submission.WaitAsync(TimeSpan.FromSeconds(5));
            await act.Should().ThrowAsync<BrokerOrderRejectedException>().WithMessage("TEST.REJECT: Broker declined this order");
            dialog.Error.Should().Contain("TEST.REJECT: Broker declined this order"); model.BrokerAccepted.Should().BeFalse();
        } else { (await submission.WaitAsync(TimeSpan.FromSeconds(5))).Should().NotBeEmpty(); model.BrokerAccepted.Should().BeTrue(); }
        dialog.Closed.Should().BeTrue(); model.RequiresSubmissionReconciliation.Should().BeFalse();
    }
    sealed class TestDialog : ITradeOrderSubmissionConfirmationService
    {
        public bool Closed; public string? Error;
        public ValueTask<TradeOrderConfirmationResult> ConfirmAsync(TradeOrderReadModel order,CancellationToken token=default)
            => ValueTask.FromResult(new TradeOrderConfirmationResult(true,TradeFillType.Broker));
        public ValueTask<TradeOrderConfirmationResult> ConfirmAsync(TradeOrderReadModel order,IReadOnlyList<TradeOrderConfirmationLeg> legs,CancellationToken token=default)
            => ConfirmAsync(order,token);
        public ValueTask CompleteSubmissionAsync(string? error) { Error=error; Closed=true; return ValueTask.CompletedTask; }
    }
}
