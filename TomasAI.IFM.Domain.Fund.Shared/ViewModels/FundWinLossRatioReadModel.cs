using MessagePack;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

/// <summary>
/// Represents the win/loss ratio and Kelly criterion values for a fund, typically used in performance analysis and risk
/// management.
/// </summary>
/// <param name="WinLossRatio">The ratio of total winning trades to total losing trades for the fund. A value greater than 1 indicates more wins
/// than losses.</param>
/// <param name="KellyCriteria">The Kelly criterion value calculated for the fund, representing the optimal proportion of capital to allocate to
/// maximize long-term growth. Must be a non-negative value.</param>
[MessagePackObject]
public record FundWinLossRatioReadModel(
    [property: Key(0)] double WinLossRatio,
    [property: Key(1)] double KellyCriteria)
{
    [IgnoreMember]
    public bool IsValid => WinLossRatio > 0;
}
