using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;

internal static class OptionVolatilityRequestValidation
{
    internal static void ValidatePage(VolatilityHistoryPageRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateEnvironment(request.Scope.Environment);
        if (request.PageSize is < 1 or > 500 || request.FromValueDate > request.ToValueDate || VolatilityCalendarBucket.From(request.FromValueDate) != request.Bucket || VolatilityCalendarBucket.From(request.ToValueDate) != request.Bucket || request.Mode == VolatilityHistoricalMode.AsKnown && request.KnownAtUtc is null || request.Mode == VolatilityHistoricalMode.Restated && request.KnownAtUtc is not null)
            throw new ArgumentException("History query must be bounded to one calendar bucket.", nameof(request));
    }

    internal static void ValidatePublication(OptionIvPublication publication)
    {
        ArgumentNullException.ThrowIfNull(publication);
        ValidateEnvironment(publication.Environment);
        if (publication.Metric.Revision < 1 || publication.Metric.PublicationSequence < 1 || (publication.Metric.Revision == 1) != (publication.Metric.SupersedesSnapshotId is null) || publication.SourceObservations.IsDefaultOrEmpty || !publication.Metric.Snapshot.SourceObservationIds.Order(StringComparer.Ordinal).SequenceEqual(publication.SourceObservations.Select(x => x.ObservationId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            throw new ArgumentException("A complete immutable publication is required.", nameof(publication));
        foreach (var observation in publication.SourceObservations)
            ValidateObservation(observation);
    }

    internal static void ValidateObservation(OptionIvObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (observation.SchemaVersion != OptionIvObservation.CurrentSchemaVersion || string.IsNullOrWhiteSpace(observation.ObservationId) || observation.ExchangeValueDate == default || observation.Revision < 1 || (observation.Revision == 1) != (observation.SupersedesObservationId is null) || observation.RecordedAtUtc.Offset != TimeSpan.Zero || observation.AvailableAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Invalid option-IV observation.", nameof(observation));
    }

    internal static void ValidateLatest(LatestVolatilityRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateEnvironment(request.Scope.Environment);
        if (request.MaximumAge <= TimeSpan.Zero || request.RequestedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("A positive configured freshness requirement is required.", nameof(request));
    }

    internal static void ValidateEnvironment(string environment) => ArgumentException.ThrowIfNullOrWhiteSpace(environment);
}
