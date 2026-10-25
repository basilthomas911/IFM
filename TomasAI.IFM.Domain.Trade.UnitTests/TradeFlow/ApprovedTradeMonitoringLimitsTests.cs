using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.State;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;
namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;
public sealed class ApprovedTradeMonitoringLimitsTests
{
    internal static (TradeOrderDefinition Order, EstablishedTradeDefinition Trade) Fixture()
    {
        var component = Guid.NewGuid(); var attempt = Guid.NewGuid();
        var legs = new (byte side, decimal strike, int quantity)[] { (1,110m,-2), (1,120m,2), (2,90m,-2), (2,80m,2) }
            .Select((x,i) => new TradeLegDefinition { TradeLegId = Guid.NewGuid(), ContractId = "option-"+i,
                ContractKey = "option-"+i, SignedQuantity = x.quantity, PutCall = x.side, Strike = x.strike,
                CashMultiplier = 50, AssetFamily = TradeAssetFamily.FuturesOption, Expiry = new(2026,11,20) }).ToArray();
        var fills = legs.Select((leg,i) => new ExecutionFillEvidence { ExecutionFillId = Guid.NewGuid(),
            ExecutionAttemptId = attempt, ComponentId = component, TradeLegId = leg.TradeLegId,
            ContractId = leg.ContractId, SignedQuantity = leg.SignedQuantity, Price = new[] { 10m,5m,12m,4m }[i],
            Commission = 1, FilledAtUtc = new(2026,10,16,14,0,0,DateTimeKind.Utc) }).ToArray();
        var order = new TradeOrderDefinition { Id = new(101,701,1901), PortfolioApprovalId = Guid.NewGuid(),
            Revision = 1, DefinitionHash = "approved-terms", PositionType = TradeOrderPositionType.Opening,
            RequiredCapital = 5000, MaximumLoss = 700, Components = [new() { ComponentId = component,
                ReservedTradeId = 1501, StrategyKind = TradeStrategyKind.IronCondor, Legs = legs }] };
        var trade = new EstablishedTradeDefinition { Id = new(101,701,1901,1501), SourceComponentId = component,
            ExecutionAttemptId = attempt, AssetFamily = TradeAssetFamily.FuturesOption, StrategyKind = TradeStrategyKind.IronCondor,
            Legs = legs, OriginalFills = fills, OpeningCommission = 4, EstablishedAtUtc = fills[0].FilledAtUtc, EvidenceRevision = 1 };
        return (order,trade);
    }
    [Fact] public void Limits_keep_approved_loss_and_capital_and_use_actual_weighted_fills()
    {
        var (order,trade) = Fixture();
        var first = trade.OriginalFills[0];
        trade = trade with { OriginalFills = [first with { SignedQuantity = -1, Price = 9, Commission = .5m },
            first with { ExecutionFillId = Guid.NewGuid(), SignedQuantity = -1, Price = 11, Commission = .5m }, ..trade.OriginalFills.Skip(1)] };
        var result = ApprovedTradeMonitoringLimits.Create(order,trade);
        result.PortfolioApprovalId.Should().Be(order.PortfolioApprovalId);
        result.TradeLimit.RiskMargin.Should().Be(5000);
        result.TradeLimit.MaxLoss.Should().Be(-700);
        result.TradeLimit.MaxProfit.Should().Be(1300);
        result.TradeLimit.MinProfitTarget.Should().Be(658);
        result.SpreadLimits.Should().HaveCount(2);
        ApprovedTradeMonitoringLimits.Create(order,trade).Should().BeEquivalentTo(result);
    }
    [Fact] public void Execution_completion_carries_the_limits_in_its_created_trade_source_snapshot()
    {
        var (order,trade) = Fixture();
        var execution = new OrderExecutionDefinition { TradeOrderId = order.Id, Order = order,
            PositionType = TradeOrderPositionType.Opening, ExecutionAttemptId = trade.ExecutionAttemptId,
            Status = OrderExecutionStatus.Submitted, Components = order.Components, Fills = trade.OriginalFills };
        var completed = TomasAI.IFM.Domain.Trade.Order.Execution.Command.Model.OrderExecutionComputation.Accept(execution,trade.EstablishedAtUtc);
        completed.Accepted.Should().BeTrue();
        completed.CreatedTrades.Single().MonitoringLimits!.TradeLimit.MaxLoss.Should().Be(-700);
    }
    [Fact] public void Snapshots_round_trip_and_pre_limit_trade_payloads_remain_readable()
    {
        var (order,trade) = Fixture();
        trade = trade with { MonitoringLimits = ApprovedTradeMonitoringLimits.Create(order,trade) };
        var bytes = MessagePack.MessagePackSerializer.Serialize(trade);
        MessagePack.MessagePackSerializer.Deserialize<EstablishedTradeDefinition>(bytes).Should().BeEquivalentTo(trade);
        var reader = new MessagePack.MessagePackReader(new System.Buffers.ReadOnlySequence<byte>(bytes));
        // Keys 16 and 17 were already added by schema v4 before this assembly migration.
        reader.ReadArrayHeader().Should().Be(18);
        var output = new System.Buffers.ArrayBufferWriter<byte>();
        var writer = new MessagePack.MessagePackWriter(output);
        writer.WriteArrayHeader(15);
        for (var index = 0; index < 15; index++) writer.WriteRaw(reader.ReadRaw());
        writer.Flush();
        MessagePack.MessagePackSerializer.Deserialize<EstablishedTradeDefinition>(output.WrittenMemory).MonitoringLimits.Should().BeNull();
    }
    [Fact] public void Wrong_approval_identity_or_missing_fills_are_rejected()
    {
        var (order,trade) = Fixture();
        FluentActions.Invoking(() => ApprovedTradeMonitoringLimits.Create(order with { PortfolioApprovalId = Guid.Empty },trade)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => ApprovedTradeMonitoringLimits.Create(order with { Id = new(102,701,1901) },trade)).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => ApprovedTradeMonitoringLimits.Create(order,trade with { OriginalFills = trade.OriginalFills.Skip(1).ToArray() })).Should().Throw<InvalidOperationException>();
        FluentActions.Invoking(() => ApprovedTradeMonitoringLimits.Create(order with { Components = [..order.Components, order.Components[0]] },trade)).Should().Throw<InvalidOperationException>();
    }
    [Fact] public void Existing_position_recovers_from_original_approval_without_current_cash()
    {
        var (order,trade) = Fixture();
        var id = StrategyPositionId.Create(trade.Id,TradeStrategyKind.IronCondor);
        var state = new IronCondorPositionCommandState();
        var open = new OpenIronCondorPositionCommand { EntityId = id, CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command,PositionActorNames.IronCondorCommand,OpenIronCondorPositionCommand.Verb,id.Format()),
            Trade = trade, EffectiveAtUtc = trade.EstablishedAtUtc };
        open.Execute(state).Success.Should().BeTrue();
        var recover = new InitializeIronCondorMonitoringCommand { EntityId = id, CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command,PositionActorNames.IronCondorCommand,InitializeIronCondorMonitoringCommand.Verb,id.Format()),
            MonitoringInitialization = new() { IronCondorTrade = trade, ApprovedOrder = order, InitializedAtUtc = trade.EstablishedAtUtc.AddDays(1) } };
        recover.Execute(state).Success.Should().BeTrue();
        state.TradeLimits!.MaxLoss.Should().Be(-700);
        state.TradeLimits.CreatedOn.Should().Be(trade.EstablishedAtUtc);
    }
}
