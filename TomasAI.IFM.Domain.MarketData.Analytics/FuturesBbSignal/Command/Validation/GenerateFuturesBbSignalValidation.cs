using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Validation;

/// <summary>Checks the source bar and corresponding EMA for Bollinger generation.</summary>
public static class GenerateFuturesBbSignalValidation
{
    /// <summary>Adds source identity errors before calculation is attempted.</summary>
    public static TErrors ValidateSources<TErrors>(
        this TErrors errors, GenerateFuturesBbSignalCommand command)

        where TErrors : class, ICollection<ValidationError>
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(errors);
        if (command.Observation is not { } bar)
        {
            errors.Add(new("BB.BAR.NULL", "A completed source bar is required."));
            return errors;
        }
        foreach (var error in new FuturesTradeSessionBarReadModelValidationRules().Execute(bar))
            errors.Add(new(error.ErrorCode, $"Observation: {error.ErrorMessage}"));
        if (!bar.IsValid || !bar.IsComplete)
            errors.Add(new("BB.BAR.INVALID", "A valid completed source bar is required."));
        if (bar.MarketSeriesIdentity != command.EntityId.MarketSeriesIdentity
            || bar.TimeFrame != command.EntityId.TimeFrame
            || !string.Equals(command.Subject.EntityId,command.EntityId.Format(),StringComparison.Ordinal))
            errors.Add(new("BB.BAR.ROUTE", "The source bar must match the command series and timeframe."));
        if (command.EmaSignal is not { } ema
            || ema.Metadata?.ObservationId != bar.ObservationId)
            errors.Add(new("BB.EMA.IDENTITY", "The EMA must describe the same source observation."));
        return errors;
    }
}
