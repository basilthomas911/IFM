using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Futures.Command;
using TomasAI.IFM.Domain.Trade.Futures.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Position;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;

namespace TomasAI.IFM.Domain.Trade.UnitTests;

public sealed class CommandHandlerConventionTests
{
    [Fact]
    public void Rejected_computation_and_execution_leave_trade_state_and_pending_events_unchanged()
    {
        var state = new FuturesTradeCommandState();
        var command = new CreateFuturesTradeCommand { CommandId = Guid.NewGuid() };
        command.Compute(state, out var tradeChange).Should().BeFalse();
        tradeChange.RejectionReason.Should().NotBeNull();
        state.EstablishedTradeDefinition.Should().BeNull();
        command.Execute(state).Success.Should().BeFalse();
        state.EstablishedTradeDefinition.Should().BeNull();
        state.Events.Should().BeEmpty();
    }

    [Fact]
    public void Event_factory_assigns_command_identity_before_state_application()
    {
        var command = new CreateFuturesTradeCommand { CommandId = Guid.NewGuid() };
        var definition = new EstablishedTradeDefinition();
        var sourceEvent = command.CreateFuturesTradeChangedEvent(
            new TomasAI.IFM.Domain.Trade.Futures.Command.Model.EstablishedTradeChange(definition));
        sourceEvent.CommandId.Should().Be(command.CommandId);
        sourceEvent.EstablishedTradeDefinition.Should().BeSameAs(definition);
    }

    [Fact]
    public void Business_named_snapshots_preserve_stored_json_names_and_messagepack_roundtrips()
    {
        AssertSnapshot(new FuturesTradeChangedEvent(), nameof(FuturesTradeChangedEvent.EstablishedTradeDefinition));
        AssertSnapshot(new FuturesPositionChangedEvent(), nameof(FuturesPositionChangedEvent.PositionSnapshot));
        AssertSnapshot(new WorkflowStrategyStateUpdatedEvent(), nameof(WorkflowStrategyStateUpdatedEvent.WorkflowDefinition));
    }

    static void AssertSnapshot<T>(T sourceEvent, string businessProperty) where T : class
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(sourceEvent);
        json.Should().Contain("\"State\":").And.NotContain($"\"{businessProperty}\":");
        var restoredJson = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json)!;
        typeof(T).GetProperty(businessProperty)!.GetValue(restoredJson).Should().NotBeNull();
        var bytes = MessagePackSerializer.Serialize(sourceEvent);
        var restoredBinary = MessagePackSerializer.Deserialize<T>(bytes);
        typeof(T).GetProperty(businessProperty)!.GetValue(restoredBinary).Should().NotBeNull();
        MessagePackSerializer.Serialize(restoredBinary).Should().Equal(bytes);
    }
}
