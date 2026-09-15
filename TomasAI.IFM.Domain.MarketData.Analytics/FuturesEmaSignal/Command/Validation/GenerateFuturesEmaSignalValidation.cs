using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Validation;

/// <summary>Checks the complete source observation for EMA generation.</summary>
public static class GenerateFuturesEmaSignalValidation
{
    /// <summary>Adds all source-bar and routing errors before the command loads state.</summary>
    public static List<ValidationError> ValidateSourceBar(
        this List<ValidationError> errors, GenerateFuturesEmaSignalCommand command)
    {
        if (command.Observation is not { } bar)
        {
            errors.Add(new("EMA.BAR.NULL", "A completed source bar is required."));
            return errors;
        }
        foreach (var error in new FuturesTradeSessionBarReadModelValidationRules().Execute(bar))
            errors.Add(new(error.ErrorCode, $"Observation: {error.ErrorMessage}"));
        if (!bar.IsValid || !bar.IsComplete)
            errors.Add(new("EMA.BAR.INVALID", "A valid completed source bar is required."));
        if (bar.MarketSeriesIdentity != command.EntityId.MarketSeriesIdentity
            || bar.TimeFrame != command.EntityId.TimeFrame
            || command.Subject.EntityId != command.EntityId.Format())
            errors.Add(new("EMA.BAR.ROUTE", "The source bar must match the command series and timeframe."));
        return errors;
    }
}
