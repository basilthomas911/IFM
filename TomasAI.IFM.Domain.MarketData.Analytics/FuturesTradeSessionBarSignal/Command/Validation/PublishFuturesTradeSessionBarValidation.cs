using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Command.Validation;

/// <summary>Accumulates deterministic ingress errors for one completed-bar Publish command.</summary>
public static class PublishFuturesTradeSessionBarValidation
{
    /// <summary>Validates the bar payload and its repeated Command routing identities.</summary>
    public static List<ValidationError> ValidatePublishBar(
        this List<ValidationError> errors,
        PublishFuturesTradeSessionBarCommand command)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(command);

        var entityErrors = new FuturesTradeSessionBarEntityIdValidationRules().Execute(command.EntityId);
        foreach (var error in entityErrors)
            errors.Add(new(error.ErrorCode, $"EntityId: {error.ErrorMessage}"));

        if (command.Bar is not { } bar)
        {
            errors.Add(new("BAR.NULL", "Bar is required."));
            return errors;
        }

        foreach (var error in new FuturesTradeSessionBarReadModelValidationRules().Execute(bar))
            errors.Add(new(error.ErrorCode, $"Bar: {error.ErrorMessage}"));
        if (!bar.IsComplete)
            errors.Add(new("BAR.INCOMPLETE", "Only a completed trade-session bar can be published."));
        if (!bar.IsValid)
            errors.Add(new("BAR.INVALID", "Only a valid trade-session bar can be published."));
        if (bar.ValidationIssues is { Length: > 0 })
            errors.Add(new("BAR.ISSUES", "A completed bar cannot contain validation issues."));
        if (bar.TradeCount <= 0 || bar.Volume <= 0)
            errors.Add(new("BAR.NO_TRADES", "A completed trade-session bar requires positive trade evidence."));

        if (entityErrors.Length == 0)
        {
            if (command.Subject.EntityId != command.EntityId.Format())
                errors.Add(new("BAR.ROUTE", "Command routing identity must match EntityId."));
            if (command.EntityId.MarketSeriesIdentity != bar.MarketSeriesIdentity
                || command.EntityId.TimeFrame != bar.TimeFrame)
                errors.Add(new("BAR.IDENTITY", "Bar series and timeframe must match EntityId."));
        }
        return errors;
    }
}
