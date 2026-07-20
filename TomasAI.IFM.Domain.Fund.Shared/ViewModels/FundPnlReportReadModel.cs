using MessagePack;

namespace TomasAI.IFM.Domain.Fund.Shared.ViewModels;

/// <summary>
/// View model representing the profit and loss (PnL) report for a fund over a specified period.
/// </summary>
/// <param name="WinRate">The percentage of winning trades.</param>
/// <param name="AverageProfit">The average profit from winning trades.</param>
/// <param name="LossRate">The percentage of losing trades.</param>
/// <param name="AverageLoss">The average loss from losing trades.</param>
/// <param name="WinLossRatio">The ratio of average profit to average loss.</param>
/// <param name="TargetSharpeRatio">The target Sharpe ratio for the fund.</param>
/// <param name="ActualSharpeRatio">The actual Sharpe ratio achieved by the fund.</param>
/// <param name="PnlAmount">The total profit and loss amount.</param>
/// <param name="PnlPercent">The profit and loss as a percentage of the initial investment.</param>
/// <param name="TradeCommission">The total trade commission incurred.</param>
[MessagePackObject]
public record FundPnlReportReadModel(
    [property: Key(0)] double WinRate,
    [property: Key(1)] decimal AverageProfit,
    [property: Key(2)] double LossRate,
    [property: Key(3)] decimal AverageLoss,
    [property: Key(4)] double WinLossRatio,
    [property: Key(5)] double TargetSharpeRatio,
    [property: Key(6)] double ActualSharpeRatio,
    [property: Key(7)] decimal PnlAmount,
    [property: Key(8)] double PnlPercent,
    [property: Key(9)] decimal TradeCommission)
{
    [IgnoreMember]
    public bool IsValid => WinRate > 0 || LossRate > 0;
}