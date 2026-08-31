using MessagePack;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

public enum LegacyTradeMatchStatus
{
    NoTradeDbDefinition = 0,
    DefinitionOnly = 1,
    PositionHistory = 2,
    FillHistory = 3,
}

[MessagePackObject(AllowPrivate = true)]
public sealed record LegacyPortfolioScopeReadModel
{
    [Key(0)] public PortfolioReadModel Portfolio { get; init; } = new();
    [Key(1)] public FundMandateReadModel[] Funds { get; init; } = [];
}

[MessagePackObject(AllowPrivate = true)]
public sealed record LegacyFundHistoryReadModel
{
    [Key(0)] public FundReadModel Fund { get; init; } = new(0, string.Empty, string.Empty, 0m, false, DateTime.MinValue, string.Empty);
    [Key(1)] public int OrderCount { get; init; }
    [Key(2)] public int CompositionTradeCount { get; init; }
    [Key(3)] public bool IsUnassigned { get; init; }
}

[MessagePackObject(AllowPrivate = true)]
public sealed record LegacyFundOrderHistoryReadModel
{
    [Key(0)] public FundOrderReadModel Order { get; init; } = null!;
    [Key(1)] public int CompositionTradeCount { get; init; }
}

[MessagePackObject(AllowPrivate = true)]
public sealed record LegacyFundTradeHistoryReadModel
{
    [Key(0)] public FundOrderTradeReadModel Composition { get; init; } = new();
    [Key(1)] public OptionTradeReadModel? TradeDbTrade { get; init; }
    [Key(2)] public LegacyTradeMatchStatus MatchStatus { get; init; }
    [Key(3)] public int FillCount { get; init; }
    [Key(4)] public int PositionCount { get; init; }
    [Key(5)] public int OptionLegCount { get; init; }

    public static LegacyTradeMatchStatus Classify(OptionTradeReadModel? trade) => trade switch
    {
        null => LegacyTradeMatchStatus.NoTradeDbDefinition,
        { TradeFills.Length: > 0 } => LegacyTradeMatchStatus.FillHistory,
        { TradePositions.Length: > 0 } => LegacyTradeMatchStatus.PositionHistory,
        _ => LegacyTradeMatchStatus.DefinitionOnly,
    };
}
