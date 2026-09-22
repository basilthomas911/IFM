using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Contracts;

public sealed class FundMandateMessagePackCompatibilityTests
{
    [Fact]
    public void Current_contract_deserializes_pre_cleanup_wire_shape()
    {
        var createdOnUtc = new DateTime(2025, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var legacy = new PreCleanupFundMandateReadModel
        {
            PortfolioId = 101,
            FundId = 202,
            FundCode = "FUND-202",
            Name = "Compatibility Fund",
            FundMandateVersion = 3,
            SchemaVersion = 2,
            TradingYear = 2025,
            OperatingState = FundOperatingState.Active,
            EffectiveFromUtc = createdOnUtc,
            DecisionHorizon = "Monthly",
            Objective = "Preserve replay compatibility",
            UnderlyingUniverse = ["ES"],
            EligibleAssetTypes = ["FutureOption"],
            PermittedDirections = ["Long", "Short"],
            PermittedConditions = ["Any"],
            PermittedTradeFamilies = ["Options"],
            CreatedOnUtc = createdOnUtc,
            CreatedBy = "compatibility-test",
            HistoricalSource = "FundLegacyDb",
            HistoricalSourceFundId = 77,
            PermittedTradeStrategyFamilies = [new(11, 4)]
        };

        var restored = MessagePackSerializer.Deserialize<FundMandateReadModel>(
            MessagePackSerializer.Serialize(legacy));

        restored.PortfolioId.Should().Be(101);
        restored.FundId.Should().Be(202);
#pragma warning disable CS0618
        restored.HistoricalSource.Should().Be("FundLegacyDb");
        restored.HistoricalSourceFundId.Should().Be(77);
#pragma warning restore CS0618
        restored.PermittedTradeStrategyFamilies.Should().Equal(new TradeStrategyFamilyReference(11, 4));
    }

    [Fact]
    public void Published_keys_remain_assigned_to_their_original_properties()
    {
#pragma warning disable CS0618
        MessagePackKey(nameof(FundMandateReadModel.HistoricalSource)).Should().Be(19);
        MessagePackKey(nameof(FundMandateReadModel.HistoricalSourceFundId)).Should().Be(20);
#pragma warning restore CS0618
        MessagePackKey(nameof(FundMandateReadModel.PermittedTradeStrategyFamilies)).Should().Be(21);
    }

    static int MessagePackKey(string propertyName) =>
        typeof(FundMandateReadModel).GetProperty(propertyName)!
            .GetCustomAttributes(typeof(KeyAttribute), inherit: false)
            .Cast<KeyAttribute>()
            .Single()
            .IntKey!.Value;

    [MessagePackObject(AllowPrivate = true)]
    internal sealed record PreCleanupFundMandateReadModel
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
        [Key(19)] public string HistoricalSource { get; init; } = string.Empty;
        [Key(20)] public int? HistoricalSourceFundId { get; init; }
        [Key(21)] public TradeStrategyFamilyReference[] PermittedTradeStrategyFamilies { get; init; } = [];
    }
}
