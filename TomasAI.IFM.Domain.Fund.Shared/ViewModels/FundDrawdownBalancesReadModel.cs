using MessagePack;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

/// <summary>
/// Represents the starting and ending balances for a specific fund's drawdown period.
/// </summary>
/// <param name="FundId">The unique identifier of the fund associated with the drawdown balances.</param>
/// <param name="StartBalance">The balance of the fund at the beginning of the drawdown period.</param>
/// <param name="EndBalance">The balance of the fund at the end of the drawdown period.</param>
[MessagePackObject]
public record FundDrawdownBalancesReadModel(
    [property: Key(0)] int FundId,
    [property: Key(1)] decimal StartBalance,
    [property: Key(2)] decimal EndBalance)
{
    [IgnoreMember]
    public bool IsValid => FundId > 0;
}
