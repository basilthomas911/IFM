using System.Buffers;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Domain.Trade.UnitTests;

public sealed class StrategyPositionCurrencyPnlTests
{
    static readonly DateOnly OpeningDate = new(2026, 10, 16);
    static readonly Guid LegId = Guid.Parse("11112233-4455-6677-8899-aabbccddeeff");
    static EstablishedTradeDefinition Trade => new()
    {
        Id = new(101, 701, 1901, 1501), EstablishedAtUtc = new(2026, 10, 16, 14, 0, 0, DateTimeKind.Utc),
        OpeningCommission = 2.60m,
        Legs = [new() { TradeLegId = LegId, ContractId = "ES", SignedQuantity = -2, CashMultiplier = 50 }]
    };
    static StrategyPositionSnapshot Position => new()
    {
        Id = new(Trade.Id, Guid.NewGuid()), ValueDate = OpeningDate, DailyPnlPoints = .60m,
        Legs = [new() { TradeLegId = LegId, ContractId = "ES", SignedQuantity = -2 }]
    };

    [Fact]
    public void Currency_is_quantity_weighted_once_and_net_of_actual_opening_commission()
    {
        var result = StrategyPositionCurrencyPnl.Apply(Position, Trade);
        result.DailyPnl.Should().Be(27.40m);
        result.DailyPnlPoints.Should().Be(.60m);
        StrategyPositionCurrencyPnl.Apply(result, Trade).Should().BeEquivalentTo(result);
    }

    [Fact]
    public void Later_dates_do_not_repeat_opening_commission_or_reuse_a_loaded_plan()
    {
        var result = StrategyPositionCurrencyPnl.Apply(Position with
        { ValueDate = new(2026, 10, 19), DailyPnlPoints = .45m, DailyPnl = 999m }, Trade);
        result.DailyPnl.Should().Be(22.50m);
    }

    [Fact]
    public void Closing_commission_is_charged_only_on_its_execution_value_date()
    {
        var result = StrategyPositionCurrencyPnl.Apply(Position with
        {
            ValueDate = new(2026, 10, 19), DailyPnlPoints = -.075m,
            ClosingFills = [new() { FilledAtUtc = new(2026, 10, 19, 14, 0, 0, DateTimeKind.Utc), Commission = 1.30m },
                new() { FilledAtUtc = new(2026, 10, 16, 14, 0, 0, DateTimeKind.Utc), Commission = 9m }]
        }, Trade);
        result.DailyPnl.Should().Be(-5.05m);
    }

    [Fact]
    public void Captured_contract_currency_evidence_is_not_replaced_by_legacy_emulator_multiplier()
    {
        var position = StrategyPositionCurrencyPnl.Apply(Position, Trade);
        var result = StrategyPositionCurrencyPnl.Apply(position, Trade with
            { Legs = [Trade.Legs[0] with { CashMultiplier = 1 }] });
        result.ContractCashMultiplier.Should().Be(50m);
        result.DailyPnl.Should().Be(27.40m);
    }

    [Fact]
    public void Missing_currency_evidence_is_unavailable_not_a_raw_point_amount()
    {
        StrategyPositionCurrencyPnl.Apply(Position).DailyPnl.Should().BeNull();
        StrategyPositionCurrencyPnl.Apply(Position, Trade with
        { Legs = [Trade.Legs[0] with { CashMultiplier = 0 }] }).DailyPnl.Should().BeNull();
    }

    [Fact]
    public void Actor_stores_currency_on_marks_and_replay_carries_commission_evidence_to_next_day()
    {
        var at = new DateTime(2026, 10, 16, 14, 0, 0, DateTimeKind.Utc);
        var trade = Trade with { OriginalFills = [new() { TradeLegId = LegId, ContractId = "ES", SignedQuantity = -2, Price = 10 }] };
        var machine = new TomasAI.IFM.Domain.Trade.Futures.Position.Model.StrategyPositionActorStateMachine();
        machine.Open(trade, Guid.NewGuid(), at).Accepted.Should().BeTrue();
        machine.Current!.DailyPnl.Should().Be(-2.60m);
        machine.UpdateLeg(LegId, 9.75m, 1, at.AddMinutes(1), 1).Accepted.Should().BeTrue();
        machine.Current!.DailyPnl.Should().Be(22.40m);
        machine.Current.DailyPnlPoints.Should().Be(.50m);
        machine.EndOfDay(OpeningDate, new(2026, 10, 16, 21, 0, 0, DateTimeKind.Utc)).Accepted.Should().BeTrue();
        var restored = new TomasAI.IFM.Domain.Trade.Futures.Position.Model.StrategyPositionActorStateMachine();
        restored.Replay(machine.Current!);
        restored.UpdateLeg(LegId, 9.50m, 2, new(2026, 10, 18, 22, 0, 0, DateTimeKind.Utc), 1).Accepted.Should().BeTrue();
        restored.Current!.DailyPnl.Should().Be(25m);
        restored.Current.DailyPnlPoints.Should().Be(.50m);
    }

    [Fact]
    public void Legacy_wire_key_18_remains_points_and_new_currency_field_round_trips()
    {
        var result = StrategyPositionCurrencyPnl.Apply(Position, Trade);
        var bytes = MessagePackSerializer.Serialize(result, MessagePackBinarySerializer.ContentOptions);
        var reader = new MessagePackReader(bytes);
        reader.ReadArrayHeader();
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(22);
        for (var index = 0; index < 22; index++) writer.WriteRaw(reader.ReadRaw());
        writer.Flush();
        var legacy = MessagePackSerializer.Deserialize<StrategyPositionSnapshot>(buffer.WrittenMemory, MessagePackBinarySerializer.ContentOptions);
        legacy.DailyPnlPoints.Should().Be(.60m);
        legacy.DailyPnl.Should().BeNull();
        var restored = MessagePackBinarySerializer.Shared.Deserialize<StrategyPositionSnapshot>(
            MessagePackBinarySerializer.Shared.Serialize(result));
        restored.DailyPnl.Should().Be(27.40m);
        restored.DailyPnlPoints.Should().Be(.60m);
    }
}
