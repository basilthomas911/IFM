using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class IronCondorMonitoringInitializationTests
{
    [Fact]
    public void Initialization_is_source_backed_and_preserves_exact_cash_multiplier_quantity_and_command_identity()
    {
        var (command, state) = Setup();
        command.Execute(state).Success.Should().BeTrue();
        state.TradeLimits!.MaxLoss.Should().Be(-2000); state.TradeLimits.MaxProfit.Should().Be(1300);
        var accepted = state.Events.Single().Should().BeOfType<IronCondorMonitoringInitializedEvent>().Subject;
        accepted.CommandId.Should().Be(command.CommandId); accepted.MonitoringInitialization.Should().BeEquivalentTo(command.MonitoringInitialization);
        var restored = MessagePackSerializer.Deserialize<IronCondorMonitoringInitializedEvent>(MessagePackSerializer.Serialize(accepted));
        var replay = new IronCondorPositionCommandState();
        replay.ReplayEvents(new DomainEventCollection([restored]));
        replay.TradeLimits.Should().BeEquivalentTo(state.TradeLimits); replay.SpreadLimits.Should().HaveCount(2);
        replay.Events.Should().BeEmpty();
    }

    [Fact]
    public void Stale_financial_observation_or_inconsistent_legs_does_not_modify_state_or_emit_an_event()
    {
        var (command, state) = Setup();
        var stale = command with { MonitoringInitialization = command.MonitoringInitialization with { FundCashAsOfUtc = command.MonitoringInitialization.InitializedAtUtc.AddMinutes(-1) } };
        stale.Execute(state).Success.Should().BeFalse(); state.Events.Should().BeEmpty(); state.TradeLimits.Should().BeNull();
        var mismatched = command with { MonitoringInitialization = command.MonitoringInitialization with
            { IronCondorTrade = command.MonitoringInitialization.IronCondorTrade with { Legs = command.MonitoringInitialization.IronCondorTrade.Legs.Select(leg => leg with { SignedQuantity = leg.SignedQuantity*2 }).ToArray() } } };
        mismatched.Execute(state).Success.Should().BeFalse(); state.Events.Should().BeEmpty(); state.TradeLimits.Should().BeNull();
    }

    static (InitializeIronCondorMonitoringCommand, IronCondorPositionCommandState) Setup()
    {
        var (legs, _) = IronCondorOptionCalculatorTests.Evidence();
        legs = legs.Select(leg => leg with { SignedQuantity = leg.SignedQuantity*2 }).ToArray();
        var tradeId = new TradeEntityId(1,2,3,4); var id = StrategyPositionId.Create(tradeId, TradeStrategyKind.IronCondor);
        var now = new DateTime(2026,9,8,16,0,0,DateTimeKind.Utc);
        var position = new StrategyPositionSnapshot { Id = id, Legs = legs.Select(leg => new StrategyPositionLeg
            { TradeLegId = leg.TradeLegId, ContractId = leg.ContractId, PutCall = leg.PutCall, Strike = leg.Strike, SignedQuantity = leg.SignedQuantity,
                OpeningPrice = leg.SignedQuantity > 0 ? 2 : leg.PutCall == 2 ? 10 : 7 }).ToArray() };
        var state = new IronCondorPositionCommandState();
        state.ReplayEvents(new DomainEventCollection([new IronCondorPositionChangedEvent { EntityId = id, PositionSnapshot = position }]));
        var command = new InitializeIronCondorMonitoringCommand { CommandId = Guid.NewGuid(), EntityId = id,
            Subject = new(ActorType.Command, PositionActorNames.IronCondorCommand, InitializeIronCondorMonitoringCommand.Verb, id.Format()),
            MonitoringInitialization = new() { IronCondorTrade = new() { Id = tradeId, StrategyKind = TradeStrategyKind.IronCondor, Legs = legs },
                FundAvailableCash = 100000, FundFinancialRevision = 1, FundCashAsOfUtc = now, RequiredCapital = 5000,
                TradeOrderRevision = 1, ValueDate = new(2026,9,8), InitializedAtUtc = now } };
        return (command,state);
    }
}
