using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Portfolio;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Services.Trade;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Services;

public sealed class PortfolioTradeOrderServiceTests
{
    [Fact]
    public async Task Closing_submission_preserves_target_and_dispatches_the_accepted_order()
    {
        var portfolio = Substitute.For<IPortfolioOrderCompositionApi>();
        var lifecycle = Substitute.For<ITradeOrderLifecycleApi>();
        var position = new PortfolioPositionReference(11,17,23,29,Guid.NewGuid());
        var candidate = new PortfolioCloseOrderCandidate { CompositionId = Guid.NewGuid(), PositionType = PortfolioExecutionPositionType.Closing,
            Position = new() { Id = position, IsOpen = true }, ValidUntilUtc = DateTime.UtcNow.AddMinutes(5) };
        var order = new PortfolioExecutionOrderInstruction { Id = new(11,17,31), PositionType = PortfolioExecutionPositionType.Closing,
            TargetPosition = position, SetupTrade = new() { OrderId = 100, TradeId = 200, FundId = 17 } };
        var completionId = Guid.NewGuid();
        portfolio.EvaluateCloseAsync(Arg.Any<EvaluatePortfolioCloseOrderCompositionCommand>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<EvaluatePortfolioCloseOrderCompositionCommand>();
            request.Access.Roles.Should().Contain("OrderCompositionClose");
            return new ServiceOk<FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,PortfolioCloseOrderCompositionFailedEvent>>(
                FunctionResult<PortfolioCloseOrderCompositionCompletedEvent,PortfolioCloseOrderCompositionFailedEvent>.Complete(new()
                { Id = completionId, OperationId = request.OperationId, PortfolioId = 11, Receipt = new()
                    { CompositionId = candidate.CompositionId, Status = PortfolioCloseOrderCompositionStatus.ExecuteTradeOrder, TradeOrder = order } }));
        });
        lifecycle.SubmitAcceptedAsync(Arg.Any<TradeOrderDefinition>(),completionId,ExecutionChannel.Broker,Arg.Any<CancellationToken>()).Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        var result = await new PortfolioTradeOrderService(portfolio,lifecycle).SubmitClosingAsync(11,candidate,ExecutionChannel.Broker);
        result.TradeOrders.Single().SetupTrade.Should().Be(order.SetupTrade);
        await lifecycle.Received(1).SubmitAcceptedAsync(Arg.Is<TradeOrderDefinition>(x => x.PositionType == TradeOrderPositionType.Closing &&
            x.TargetPositionId.HasValue && x.TargetPositionId.Value.Trade.TradeId == 29),completionId,ExecutionChannel.Broker,Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitOpeningAsync_dispatches_every_portfolio_accepted_order()
    {
        var portfolio = Substitute.For<IPortfolioOrderCompositionApi>();
        var lifecycle = Substitute.For<ITradeOrderLifecycleApi>();
        var candidate = Candidate();
        var orders = new[] { Order(101), Order(102) };
        var completed = Completed(candidate, orders, PortfolioOrderCompositionStatus.ExecuteTradeOrders);
        portfolio.EvaluateAsync(Arg.Any<EvaluatePortfolioOrderCompositionCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => Success(completed with
            {
                OperationId = call.Arg<EvaluatePortfolioOrderCompositionCommand>().OperationId
            }));
        lifecycle.SubmitAcceptedAsync(Arg.Any<TradeOrderDefinition>(), completed.Id,
                ExecutionChannel.Manual, Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<Guid>(Guid.NewGuid()));
        var service = new PortfolioTradeOrderService(portfolio, lifecycle);

        var result = await service.SubmitOpeningAsync(11, candidate, ExecutionChannel.Manual);

        result.PortfolioEventId.Should().Be(completed.Id);
        result.Status.Should().Be(PortfolioOrderCompositionStatus.ExecuteTradeOrders);
        result.TradeOrders.Should().Equal(orders);
        await lifecycle.Received(1).SubmitAcceptedAsync(orders[0], completed.Id,
            ExecutionChannel.Manual, Arg.Any<CancellationToken>());
        await lifecycle.Received(1).SubmitAcceptedAsync(orders[1], completed.Id,
            ExecutionChannel.Manual, Arg.Any<CancellationToken>());
        await portfolio.Received(1).EvaluateAsync(
            Arg.Is<EvaluatePortfolioOrderCompositionCommand>(
                (EvaluatePortfolioOrderCompositionCommand? request) =>
                request != null
                && request.PortfolioId == 11
                && request.Body == candidate
                && request.InputSha256.Length == 64
                && request.Access.PortfolioIds!.SequenceEqual(new[] { 11 })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitOpeningAsync_returns_no_trade_without_dispatching_an_order()
    {
        var portfolio = Substitute.For<IPortfolioOrderCompositionApi>();
        var lifecycle = Substitute.For<ITradeOrderLifecycleApi>();
        var candidate = Candidate();
        var completed = Completed(candidate, [], PortfolioOrderCompositionStatus.NoTradeOrders);
        portfolio.EvaluateAsync(Arg.Any<EvaluatePortfolioOrderCompositionCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => Success(completed with
            {
                OperationId = call.Arg<EvaluatePortfolioOrderCompositionCommand>().OperationId
            }));
        var service = new PortfolioTradeOrderService(portfolio, lifecycle);

        var result = await service.SubmitOpeningAsync(11, candidate, ExecutionChannel.Manual);

        result.Status.Should().Be(PortfolioOrderCompositionStatus.NoTradeOrders);
        result.TradeOrders.Should().BeEmpty();
        await lifecycle.DidNotReceiveWithAnyArgs().SubmitAcceptedAsync(
            default!, default, default, default);
    }

    [Fact]
    public async Task SubmitOpeningAsync_reports_the_accepted_order_that_could_not_start()
    {
        var portfolio = Substitute.For<IPortfolioOrderCompositionApi>();
        var lifecycle = Substitute.For<ITradeOrderLifecycleApi>();
        var candidate = Candidate();
        var order = Order(101);
        var completed = Completed(candidate, [order], PortfolioOrderCompositionStatus.ExecuteTradeOrders);
        portfolio.EvaluateAsync(Arg.Any<EvaluatePortfolioOrderCompositionCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => Success(completed with
            {
                OperationId = call.Arg<EvaluatePortfolioOrderCompositionCommand>().OperationId
            }));
        lifecycle.SubmitAcceptedAsync(order, completed.Id, ExecutionChannel.Broker, Arg.Any<CancellationToken>())
            .Returns(new ServiceFailed<Guid>(905, "execution unavailable"));
        var service = new PortfolioTradeOrderService(portfolio, lifecycle);

        var action = () => service.SubmitOpeningAsync(11, candidate, ExecutionChannel.Broker);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*11.17.101*905*execution unavailable*");
    }

    [Fact]
    public async Task SubmitOpeningAsync_rejects_an_inconsistent_portfolio_receipt_before_dispatch()
    {
        var portfolio = Substitute.For<IPortfolioOrderCompositionApi>();
        var lifecycle = Substitute.For<ITradeOrderLifecycleApi>();
        var candidate = Candidate();
        var completed = Completed(candidate, [], PortfolioOrderCompositionStatus.ExecuteTradeOrders);
        portfolio.EvaluateAsync(Arg.Any<EvaluatePortfolioOrderCompositionCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => Success(completed with
            {
                OperationId = call.Arg<EvaluatePortfolioOrderCompositionCommand>().OperationId
            }));
        var service = new PortfolioTradeOrderService(portfolio, lifecycle);

        var action = () => service.SubmitOpeningAsync(11, candidate, ExecutionChannel.Manual);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inconsistent order-composition status*");
        await lifecycle.DidNotReceiveWithAnyArgs().SubmitAcceptedAsync(
            default!, default, default, default);
    }

    static PortfolioOrderCandidate Candidate() => new()
    {
        CompositionId = Guid.NewGuid(),
        PositionType = PortfolioExecutionPositionType.Opening,
        ValidUntilUtc = DateTime.UtcNow.AddMinutes(5)
    };

    static TradeOrderDefinition Order(int orderId) => new()
    {
        Id = new TradeOrderId(11, 17, orderId),
        PositionType = TradeOrderPositionType.Opening
    };

    static PortfolioOrderCompositionCompletedEvent Completed(
        PortfolioOrderCandidate candidate,
        TradeOrderDefinition[] orders,
        PortfolioOrderCompositionStatus status) => new()
        {
            Id = Guid.NewGuid(),
            OperationId = Guid.NewGuid(),
            PortfolioId = 11,
            Receipt = new PortfolioOrderCompositionReceipt
            {
                PortfolioId = 11,
                CompositionId = candidate.CompositionId,
                Status = status,
                TradeOrders = [.. orders.Select(order => order.ToPortfolioInstruction())]
            }
        };

    static ServiceOk<FunctionResult<PortfolioOrderCompositionCompletedEvent, PortfolioOrderCompositionFailedEvent>>
        Success(PortfolioOrderCompositionCompletedEvent completed) => new(
            FunctionResult<PortfolioOrderCompositionCompletedEvent, PortfolioOrderCompositionFailedEvent>
                .Complete(completed));
}
