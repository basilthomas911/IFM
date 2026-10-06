using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Feed.Command;
using TomasAI.IFM.Domain.MarketData.Feed.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.Architecture;

public sealed class FeedCommandConventionTests
{
    private static readonly DateOnly ValueDate = new(2026, 10, 5);
    private static readonly TradeEntityId TradeId = new(1, 2, 3, 4);

    [Theory]
    [InlineData("AddTradeLiveFeed", true)]
    [InlineData("TurnTradeLiveFeedOn", true)]
    [InlineData("RemoveTradeLiveFeed", false)]
    [InlineData("TurnTradeLiveFeedOff", false)]
    [InlineData("HaltTradeLiveFeed", false)]
    public void Rejected_trade_feed_change_keeps_state_and_pending_events(string operation, bool initiallyOn)
    {
        var state = new MarketDataFeedCommandState();
        var initial = new AddTradeLiveFeedCommand(TradeId, ValueDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, AddTradeLiveFeedCommand.Actor, AddTradeLiveFeedCommand.Verb, TradeId.Format())
        };
        state.Id = initial.Subject.ThreadId;
        if (initiallyOn)
            initial.Execute(state).Success.Should().BeTrue();
        var pending = state.Events.ToArray();
        var commandType = typeof(AddTradeLiveFeedCommand).Assembly.GetType(
            $"TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands.{operation}Command")!;
        var command = Activator.CreateInstance(commandType)!;
        commandType.GetProperty("CommandId")!.SetValue(command, Guid.NewGuid());
        commandType.GetProperty("EntityId")!.SetValue(command, TradeId);
        commandType.GetProperty("ValueDate")?.SetValue(command, ValueDate);
        commandType.GetProperty("Subject")!.SetValue(command, new ActorSubject(
            ActorType.Command, AddTradeLiveFeedCommand.Actor, operation, TradeId.Format()));
        var handler = typeof(AddTradeLiveFeed).Assembly.GetType(
            $"TomasAI.IFM.Domain.MarketData.Feed.Command.{operation}")!;

        var result = (ServiceResult<GuidResult>)handler.GetMethod("Execute")!.Invoke(null, [command, state])!;

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain(initiallyOn ? "already on" : operation == "HaltTradeLiveFeed" ? "not on" : "already off");
        state.Events.Should().Equal(pending);
        var subsequent = initial.Execute(state);
        subsequent.Success.Should().Be(!initiallyOn);
    }

    [Fact]
    public void Start_factory_carries_command_identity_before_Update_and_serialization()
    {
        var command = new StartMarketDataFeedCommand([], ValueDate, false)
        {
            CommandId = Guid.NewGuid(),
            EntityId = new MarketDataFeedId(ValueDate),
            Subject = new(ActorType.Command, StartMarketDataFeedCommand.Actor, StartMarketDataFeedCommand.Verb, new MarketDataFeedId(ValueDate).Format())
        };
        object?[] computeArguments = [command, null];
        typeof(StartMarketDataFeed).GetMethod("Compute", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, computeArguments).Should().Be(true);
        var started = (MarketDataFeedStartedEvent)typeof(StartMarketDataFeed)
            .GetMethod("CreateMarketDataFeedStartedEvent", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [command, computeArguments[1]])!;

        started.CommandId.Should().Be(command.CommandId);
        started.ValueDate.Should().Be(ValueDate);
        var restored = MessagePackSerializer.Deserialize<MarketDataFeedStartedEvent>(MessagePackSerializer.Serialize(started));
        restored.CommandId.Should().Be(command.CommandId);
        restored.ValueDate.Should().Be(ValueDate);
    }
}
