using FluentValidation;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;

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
            (outcome.Dataset != MarketDataDownloadDataset.TreasuryCurve || (string.Equals((scope),("US"),StringComparison.Ordinal))));
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
        if ((string.Equals((scope),("ALL"),StringComparison.Ordinal))) return true;
        if (string.IsNullOrWhiteSpace(scope)) return false;
        var countries = scope.Split(',');
        return countries.All(country => country.Length is >= 2 and <= 3 && country.All(char.IsAsciiLetter) && (string.Equals((country),(country.ToUpperInvariant()),StringComparison.Ordinal))) &&
            (string.Equals((scope),(string.Join(",", countries.Distinct(StringComparer.Ordinal).OrderBy(country => country, StringComparer.Ordinal))),StringComparison.Ordinal));
    }
}
