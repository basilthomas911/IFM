using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Fund.Command.Validation;

/// <summary>
/// Fund-command scalar and cross-parameter validation. Intrinsic validation for
/// identifiers and read models remains with those types.
/// </summary>
public static class FundCommandValidationExtensions
{
    public static List<ValidationError> ValidateTradeState(
        this List<ValidationError> validationErrors,
        TradeState tradeState,
        string commandName)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (!Enum.IsDefined(tradeState))
            validationErrors.Add(new($"{commandName}.TradeState is invalid"));
        return validationErrors;
    }

    public static List<ValidationError> ValidateFundTimePeriod(
        this List<ValidationError> validationErrors,
        TimeFrameType fundTimePeriod,
        string commandName)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (!Enum.IsDefined(fundTimePeriod) || fundTimePeriod == TimeFrameType.None)
            validationErrors.Add(new($"{commandName}.FundTimePeriod is invalid"));
        return validationErrors;
    }

    /// <summary>
    /// Cross-checks duplicated fund identity only after both values pass their
    /// intrinsic positive-value rules, avoiding duplicate derivative errors.
    /// </summary>
    public static List<ValidationError> ValidateFundIdMatches(
        this List<ValidationError> validationErrors,
        FundId? entityId,
        int? payloadFundId,
        string payloadName,
        string commandName)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (entityId is { Id: > 0 }
            && payloadFundId is > 0
            && entityId.Id != payloadFundId.Value)
        {
            validationErrors.Add(new(
                $"{commandName}.EntityId.Id must match {payloadName}.FundId"));
        }

        return validationErrors;
    }
}
