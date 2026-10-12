using FluentValidation;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

/// <summary>Intrinsic validation for durable download-log identities.</summary>
public static class DownloadLogIdValidation
{
    /// <summary>Appends identity errors without throwing for missing or empty identifiers.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "The established validation/read-model API exposes a mutable List to collect results; changing it would break consumers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the concrete collection contract consumed by existing query, event and validation adapters.")]
    public static List<ValidationError> ValidateDownloadLogId(this List<ValidationError> errors, DownloadLogId? id, string commandName)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (id is null || id.ImportCommandId == Guid.Empty)
            errors.Add(new ValidationError($"{commandName}: DownloadLog import identity is required."));
        return errors;
    }
}
