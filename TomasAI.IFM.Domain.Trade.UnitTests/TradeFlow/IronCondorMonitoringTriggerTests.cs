using IronCondorTradePlanId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;
using FluentAssertions;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorMonitoringTriggerTests
{
    [Fact]
    public async Task Retired_or_superseded_input_notification_cannot_request_a_plan()
    {
        var context = Substitute.For<IIronCondorTradePositionRealtimeContext>();
        var changed = Observation();
        context.IsCurrentMonitoringPosition(changed.PositionSnapshot, changed.MonitoringGenerationId).Returns(false);
        await changed.ExecuteAsync(context);
        context.ReceivedCalls().Should().OnlyContain(call => call.GetMethodInfo().Name == nameof(context.IsCurrentMonitoringPosition));
    }

    [Fact]
    public async Task Pending_references_publish_position_then_loaded_notification_runs_forward_price_without_another_tick()
    {
        var context = Substitute.For<IIronCondorTradePositionRealtimeContext>();
        var changed = Observation();
        context.TimeProvider.Returns(TimeProvider.System);
        await new IronCondorPositionChangedEvent { Id = Guid.NewGuid(), EntityId = changed.EntityId,
            PositionSnapshot = changed.PositionSnapshot }.ExecuteAsync(context);
        context.ReceivedCalls().Should().NotContain(call => call.GetMethodInfo().Name == "RequestFunctionAsync");
        await context.Received(1).PublishRealtimeAsync(changed.PositionSnapshot, null);
        context.ClearReceivedCalls();
        var forward = new IronCondorForwardPriceCalculatedEvent { CommandId = Guid.NewGuid(),
            IronCondorForwardPrice = new() { ForwardPrice = 12m } };
        context.RequestFunctionAsync<CalculateIronCondorForwardPriceCommand, IronCondorTradePlanId,
            FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>(
            Arg.Any<CalculateIronCondorForwardPriceCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<FunctionResult<IronCondorForwardPriceCalculatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>(new(forward, null)));
        context.IsCurrentMonitoringPosition(changed.PositionSnapshot, changed.MonitoringGenerationId).Returns(true);
        context.Parameters.Returns(new TradePlanParameters());
        var complete = new FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>(
            new IronCondorTradePlanUpdatedEvent(), null);
        context.RequestFunctionAsync<UpdateIronCondorTradePlanCommand, IronCondorTradePlanId,
            FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>(
            Arg.Any<UpdateIronCondorTradePlanCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>(complete));
        await changed.ExecuteAsync(context);
        var requests = context.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "RequestFunctionAsync")
            .Select(call => call.GetArguments()[0]).ToArray();
        requests.Should().HaveCount(2);
        var forwardRequest = requests[0].Should().BeOfType<CalculateIronCondorForwardPriceCommand>().Subject;
        forwardRequest.SourceEventId.Should().Be(changed.Id);
        forwardRequest.IronCondorForwardPriceInputs.TradeDate.Should().Be(changed.IronCondorTradePlanInputs.TradeDate!.Value);
        forwardRequest.IronCondorForwardPriceInputs.MaturityDate.Should().Be(changed.IronCondorTradePlanInputs.MaturityDate!.Value);
        var command = requests[1].Should().BeOfType<UpdateIronCondorTradePlanCommand>().Subject;
        command.UseForwardPriceFunction.Should().BeTrue();
        command.IronCondorForwardPriceCalculated.Should().BeSameAs(forward);
        command.ForwardPriceFailure.Should().BeNull();
        command.IronCondorTradePlanInputs.Should().BeSameAs(changed.IronCondorTradePlanInputs);
        command.Position.Should().BeSameAs(changed.PositionSnapshot); command.SourceEventId.Should().Be(changed.Id);
        command.RequestedAtUtc.Should().Be(changed.ReceivedOn);
        await context.Received(1).PublishRealtimeAsync(changed.PositionSnapshot, complete.Completed!.Plan);
        context.ReceivedCalls().Should().OnlyContain(call => new[] { "IsCurrentMonitoringPosition", "get_Parameters", "RequestFunctionAsync", "PublishRealtimeAsync" }.Contains(call.GetMethodInfo().Name));
        MessagePackSerializer.Deserialize<IronCondorMonitoringInputsChangedEvent>(MessagePackSerializer.Serialize(changed)).Should().BeEquivalentTo(changed);
    }

    static IronCondorMonitoringInputsChangedEvent Observation()
    {
        var id = StrategyPositionId.Create(new(1,2,3,4), TradeStrategyKind.IronCondor);
        return new() { Id = Guid.NewGuid(), EntityId = id, ValueDate = new(2026,10,7), MonitoringGenerationId = Guid.NewGuid(),
            PositionSnapshot = new() { Id = id, PositionSequence = 3, RouteGeneration = 1,
                AsOfUtc = new(2026,10,7,12,0,0,DateTimeKind.Utc) },
            IronCondorTradePlanInputs = new() { TradeDate = new(2026,10,1), MaturityDate = new(2026,11,20) }, ReceivedOn = new(2026,10,7,12,0,0,DateTimeKind.Utc) };
    }
}
