using FluentValidation;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

/// <summary>Adapts structured terminal-evidence validation to actor error lists.</summary>
public static class MarketDataDownloadOutcomeValidation
{
    static readonly MarketDataDownloadOutcomeRules Rules = new();

    /// <summary>Appends all payload errors; null payloads produce a validation error.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "The established validation/read-model API exposes a mutable List to collect results; changing it would break consumers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the concrete collection contract consumed by existing query, event and validation adapters.")]
    public static List<ValidationError> ValidateDownloadOutcome(this List<ValidationError> errors, MarketDataDownloadOutcome? outcome)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (outcome is null) errors.Add(new ValidationError("DownloadLog.Outcome is required."));
        else errors.AddRange(Rules.Validate(outcome).Errors.Select(error => new ValidationError($"DownloadLog.Outcome.{error.PropertyName}: {error.ErrorMessage}")));
        return errors;
    }
}
