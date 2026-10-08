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
    public async Task Refreshed_inputs_use_the_same_monitoring_function_without_any_order_request()
    {
        var context = Substitute.For<IIronCondorTradePositionRealtimeContext>();
        var changed = Observation();
        context.IsCurrentMonitoringPosition(changed.PositionSnapshot, changed.MonitoringGenerationId).Returns(true);
        context.Parameters.Returns(new TradePlanParameters());
        var complete = new FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>(
            new IronCondorTradePlanUpdatedEvent(), null);
        context.RequestFunctionAsync<UpdateIronCondorTradePlanCommand, IronCondorTradePlanId,
            FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>(
            Arg.Any<UpdateIronCondorTradePlanCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceOk<FunctionResult<IronCondorTradePlanUpdatedEvent, TradePlanFailedEvent<IronCondorTradePlanId>>>(complete));
        await changed.ExecuteAsync(context);
        var call = context.ReceivedCalls().Single(call => call.GetMethodInfo().Name == "RequestFunctionAsync");
        var command = (UpdateIronCondorTradePlanCommand)call.GetArguments()[0]!;
        command.IronCondorTradePlanInputs.Should().BeSameAs(changed.IronCondorTradePlanInputs);
        command.Position.Should().BeSameAs(changed.PositionSnapshot); command.SourceEventId.Should().Be(changed.Id);
        command.RequestedAtUtc.Should().Be(changed.ReceivedOn);
        context.ReceivedCalls().Should().OnlyContain(call => new[] { "IsCurrentMonitoringPosition", "get_Parameters", "RequestFunctionAsync" }.Contains(call.GetMethodInfo().Name));
        MessagePackSerializer.Deserialize<IronCondorMonitoringInputsChangedEvent>(MessagePackSerializer.Serialize(changed)).Should().BeEquivalentTo(changed);
    }

    static IronCondorMonitoringInputsChangedEvent Observation()
    {
        var id = StrategyPositionId.Create(new(1,2,3,4), TradeStrategyKind.IronCondor);
        return new() { Id = Guid.NewGuid(), EntityId = id, ValueDate = new(2026,10,7), MonitoringGenerationId = Guid.NewGuid(),
            PositionSnapshot = new() { Id = id, PositionSequence = 3, RouteGeneration = 1 }, ReceivedOn = new(2026,10,7,12,0,0,DateTimeKind.Utc) };
    }
}
