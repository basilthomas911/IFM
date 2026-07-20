using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.CommandParameters;

/// <summary>
/// Represents the parameters required to generate the maximum profit for a fund order, including the fund order details and an associated error code.
/// </summary>
/// <param name="FundOrder">The fund order details to evaluate for maximum profit. Cannot be null.</param>
/// <param name="TimePeriod">The time period for evaluating the maximum profit. Specifies the granularity of the analysis.</param>
/// <param name="ErrorCode">The error code associated with the generate max profit operation. Used to indicate specific error conditions or statuses.</param>
public record GenerateFundMaxProfitParameter(FundOrderReadModel FundOrder, TradeTimePeriodType TimePeriod, int ErrorCode)
    : ICommandParameter
{
}
