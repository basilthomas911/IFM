using FluentValidation;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

/// <summary>Intrinsic validation for durable download-log identities.</summary>
public static class DownloadLogIdValidation
{
    /// <summary>Appends identity errors without throwing for missing or empty identifiers.</summary>
    public static List<ValidationError> ValidateDownloadLogId(this List<ValidationError> errors, DownloadLogId? id, string commandName)
    {
        if (id is null || id.ImportCommandId == Guid.Empty)
            errors.Add(new ValidationError($"{commandName}: DownloadLog import identity is required."));
        return errors;
    }
}

/// <summary>Validates every property of terminal download evidence without throwing for ordinary invalid data.</summary>
public sealed class MarketDataDownloadOutcomeRules : AbstractValidator<MarketDataDownloadOutcome>
{
    /// <summary>Defines schema, partition, identity, timing, count and diagnostic constraints.</summary>
    public MarketDataDownloadOutcomeRules()
    {
        RuleFor(outcome => outcome.SchemaVersion).Equal((short)1);
        RuleFor(outcome => outcome.Dataset).Must(dataset => dataset is MarketDataDownloadDataset.EconomicCalendar or MarketDataDownloadDataset.TreasuryCurve);
        RuleFor(outcome => outcome.Provider).Must((outcome, provider) => MarketDataDownloadOutcome.IsSupportedProvider(outcome.Dataset, provider));
        RuleFor(outcome => outcome.ValueDate).NotEqual(default(DateOnly));
        RuleFor(outcome => outcome.Scope).Must((outcome, scope) => IsCanonicalScope(scope) &&
            (outcome.Dataset != MarketDataDownloadDataset.TreasuryCurve || scope == "US"));
        RuleFor(outcome => outcome.ImportCommandId).NotEmpty();
        RuleFor(outcome => outcome.SourceTerminalEventId).NotEmpty();
        RuleFor(outcome => outcome.RequestedAtUtc).Must(IsMillisecondUtc);
        RuleFor(outcome => outcome.StartedAtUtc).Must(IsMillisecondUtc).GreaterThanOrEqualTo(outcome => outcome.RequestedAtUtc);
        RuleFor(outcome => outcome.FinishedAtUtc).Must(IsMillisecondUtc).GreaterThanOrEqualTo(outcome => outcome.StartedAtUtc);
        RuleFor(outcome => outcome.Status).Must(status => status is MarketDataDownloadStatus.Completed or MarketDataDownloadStatus.Failed);
        RuleFor(outcome => outcome.DownloadedRecordCount).Must((outcome, count) => count is null ? outcome.Status != MarketDataDownloadStatus.Completed : count >= 0);
        RuleFor(outcome => outcome.PersistedRecordCount).Must((outcome, count) => count is null ? outcome.Status != MarketDataDownloadStatus.Completed : count >= 0);
        RuleFor(outcome => outcome.ElapsedMilliseconds).GreaterThanOrEqualTo(0);
        RuleFor(outcome => outcome.ErrorCode).Must((outcome, code) => (code?.Length ?? 0) <= 128 &&
            (outcome.Status == MarketDataDownloadStatus.Completed ? code is null : outcome.Status != MarketDataDownloadStatus.Failed || !string.IsNullOrWhiteSpace(code)));
        RuleFor(outcome => outcome.ErrorMessage).Must((outcome, message) => (message?.Length ?? 0) <= 512 &&
            (outcome.Status == MarketDataDownloadStatus.Completed ? message is null : outcome.Status != MarketDataDownloadStatus.Failed || !string.IsNullOrWhiteSpace(message)));
    }

    /// <summary>Checks UTC millisecond precision without timestamp normalization or exceptions.</summary>
    static bool IsMillisecondUtc(DateTime time) => time != default && time.Kind == DateTimeKind.Utc && time.Ticks % TimeSpan.TicksPerMillisecond == 0;

    /// <summary>Checks the established canonical country-scope format without throwing.</summary>
    static bool IsCanonicalScope(string? scope)
    {
        if (scope == "ALL") return true;
        if (string.IsNullOrWhiteSpace(scope)) return false;
        var countries = scope.Split(',');
        return countries.All(country => country.Length is >= 2 and <= 3 && country.All(char.IsAsciiLetter) && country == country.ToUpperInvariant()) &&
            scope == string.Join(",", countries.Distinct(StringComparer.Ordinal).OrderBy(country => country, StringComparer.Ordinal));
    }
}

/// <summary>Adapts structured terminal-evidence validation to actor error lists.</summary>
public static class MarketDataDownloadOutcomeValidation
{
    static readonly MarketDataDownloadOutcomeRules Rules = new();

    /// <summary>Appends all payload errors; null payloads produce a validation error.</summary>
    public static List<ValidationError> ValidateDownloadOutcome(this List<ValidationError> errors, MarketDataDownloadOutcome? outcome)
    {
        if (outcome is null) errors.Add(new ValidationError("DownloadLog.Outcome is required."));
        else errors.AddRange(Rules.Validate(outcome).Errors.Select(error => new ValidationError($"DownloadLog.Outcome.{error.PropertyName}: {error.ErrorMessage}")));
        return errors;
    }
}
