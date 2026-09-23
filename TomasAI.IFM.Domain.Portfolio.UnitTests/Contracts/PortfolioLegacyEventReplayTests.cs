using System.Buffers;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Contracts;

public sealed class PortfolioLegacyEventReplayTests
{
    [Fact]
    public void Legacy_fund_event_map_and_original_mandate_shape_replay_as_current_contract()
    {
        var occurredOnUtc = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var payload = SerializeEnvelope(new LegacyFundMandateCreated
        {
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            Revision = 1,
            OccurredOnUtc = occurredOnUtc,
            Principal = "compatibility-test",
            Mandate = new LegacyFundMandate
            {
                PortfolioId = 1201,
                FundId = 5401,
                FundCode = "FUND-5401",
                Name = "Legacy Fund",
                FundMandateVersion = 1,
                SchemaVersion = 2,
                TradingYear = 2026,
                OperatingState = FundOperatingState.Active,
                EffectiveFromUtc = occurredOnUtc,
                DecisionHorizon = "Daily",
                Objective = "Replay",
                UnderlyingUniverse = ["SPX"],
                EligibleAssetTypes = ["Option"],
                PermittedDirections = ["Long", "Short"],
                PermittedConditions = ["Any"],
                PermittedTradeFamilies = ["Options"],
                CreatedOnUtc = occurredOnUtc,
                CreatedBy = "compatibility-test",
                PermittedTradeStrategyFamilies = [new TradeStrategyFamilyReference(11, 4)],
            },
        });
        var row = new EventStreamReadModel
        {
            EventVersion = 41,
            StreamVersion = 1,
            EventTypeName = "TomasAI.IFM.Domain.Portfolio.Command.Model.FundMandateCreated, "
                + "TomasAI.IFM.Domain.Portfolio, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
            EventData = payload,
        };

        var replayed = row.ToDomainEvent().Should().BeOfType<FundMandateCreatedEvent>().Subject;

        replayed.EventId.Should().Be(41);
        replayed.Mandate.PortfolioId.Should().Be(1201);
        replayed.Mandate.FundId.Should().Be(5401);
        replayed.Mandate.PermittedTradeStrategyFamilies.Should().Equal(new TradeStrategyFamilyReference(11, 4));
    }

    static byte[] SerializeEnvelope<T>(T value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(3);
        writer.Write(1);
        writer.Write(false);
        MessagePackSerializer.Serialize(ref writer, value);
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    [MessagePackObject(keyAsPropertyName: true)]
    public sealed record LegacyFundMandateCreated
    {
        public Guid Id { get; init; }
        public Guid CommandId { get; init; }
        public long Revision { get; init; }
        public DateTime OccurredOnUtc { get; init; }
        public string Principal { get; init; } = string.Empty;
        public LegacyFundMandate Mandate { get; init; } = new();
    }

    [MessagePackObject]
    public sealed record LegacyFundMandate
    {
        [Key(0)] public int PortfolioId { get; init; }
        [Key(1)] public int FundId { get; init; }
        [Key(2)] public string FundCode { get; init; } = string.Empty;
        [Key(3)] public string Name { get; init; } = string.Empty;
        [Key(4)] public long FundMandateVersion { get; init; }
        [Key(5)] public int SchemaVersion { get; init; }
        [Key(6)] public int TradingYear { get; init; }
        [Key(7)] public FundOperatingState OperatingState { get; init; }
        [Key(8)] public DateTime EffectiveFromUtc { get; init; }
        [Key(9)] public DateTime? EffectiveUntilUtc { get; init; }
        [Key(10)] public string DecisionHorizon { get; init; } = string.Empty;
        [Key(11)] public string Objective { get; init; } = string.Empty;
        [Key(12)] public string[] UnderlyingUniverse { get; init; } = [];
        [Key(13)] public string[] EligibleAssetTypes { get; init; } = [];
        [Key(14)] public string[] PermittedDirections { get; init; } = [];
        [Key(15)] public string[] PermittedConditions { get; init; } = [];
        [Key(16)] public string[] PermittedTradeFamilies { get; init; } = [];
        [Key(17)] public DateTime CreatedOnUtc { get; init; }
        [Key(18)] public string CreatedBy { get; init; } = string.Empty;
        [Key(19)] public TradeStrategyFamilyReference[] PermittedTradeStrategyFamilies { get; init; } = [];
    }
}