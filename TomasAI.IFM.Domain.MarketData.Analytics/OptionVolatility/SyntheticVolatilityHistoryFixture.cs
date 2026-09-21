using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

namespace TomasAI.IFM.Domain.MarketData.Analytics.OptionVolatility;

public enum SyntheticVolatilityFixtureEventKind : byte
{
    Observation = 1,
    FuturesRoll = 2,
    Correction = 3
}

public sealed record SyntheticVolatilityFixtureEvent(
    SyntheticVolatilityFixtureEventKind Kind,
    DateTimeOffset OccurredAtUtc,
    string Description,
    string? ObservationId);

/// <summary>
/// Deterministic, explicitly synthetic, market-closed history used for offline qualification.
/// It contains the complete configured prior-session window plus a current observation, a futures
/// roll boundary, and an append-only correction learned after the original observation.
/// </summary>
public sealed record SyntheticVolatilityHistoryFixture(
    bool IsSynthetic,
    string Environment,
    string MarketState,
    DateTimeOffset VirtualNowUtc,
    VolatilitySeriesDefinition Definition,
    ImmutableArray<OptionIvObservation> AppendOnlyObservations,
    ImmutableArray<SyntheticVolatilityFixtureEvent> Events)
{
    public ImmutableArray<OptionIvObservation> AsKnownAt(DateTimeOffset knownAtUtc) =>
        AppendOnlyObservations.Where(x => x.AvailableAtUtc <= knownAtUtc)
            .GroupBy(x => (x.ExchangeValueDate, x.SamplingSlot))
            .Select(x => x.OrderByDescending(y => y.Revision).First())
            .OrderBy(x => x.ExchangeValueDate).ToImmutableArray();
}

public static class SyntheticVolatilityHistoryFixtureFactory
{
    public static SyntheticVolatilityHistoryFixture CreateClosedMarket(
        VolatilitySeriesDefinition definition,
        TimeProvider clock,
        decimal startingIv = 0.18m)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(clock);
        if (!StringComparer.Ordinal.Equals(definition.DataIdentity.Environment, "simulation"))
            throw new ArgumentException("Synthetic fixtures require the isolated simulation environment.", nameof(definition));
        if (definition.Metrics.HistoricalLookbackSessions < 2 || startingIv <= 0)
            throw new ArgumentOutOfRangeException(nameof(definition));

        var now = clock.GetUtcNow();
        var currentDate = PreviousWeekday(DateOnly.FromDateTime(now.UtcDateTime));
        var dates = PriorWeekdays(currentDate, definition.Metrics.HistoricalLookbackSessions)
            .Append(currentDate).ToArray();
        var rollIndex = Math.Max(1, dates.Length / 2);
        var observations = ImmutableArray.CreateBuilder<OptionIvObservation>(dates.Length + 1);
        var events = ImmutableArray.CreateBuilder<SyntheticVolatilityFixtureEvent>(dates.Length + 2);
        for (var index = 0; index < dates.Length; index++)
        {
            var underlying = index < rollIndex ? "ESU6" : "ESZ6";
            var available = now.AddDays(index - dates.Length).AddHours(-1);
            var id = $"synthetic-{dates[index]:yyyyMMdd}-r1";
            var observation = Observation(definition, id, dates[index], startingIv + index * 0.001m,
                underlying, 1, null, available);
            observations.Add(observation);
            events.Add(new(SyntheticVolatilityFixtureEventKind.Observation, available,
                $"SYNTHETIC daily fixture observation for {underlying}.", id));
            if (index == rollIndex)
                events.Add(new(SyntheticVolatilityFixtureEventKind.FuturesRoll, available,
                    "SYNTHETIC configured futures roll ESU6 -> ESZ6; stable series identity retained.", id));
        }

        var correctedIndex = Math.Max(1, rollIndex - 1);
        var original = observations[correctedIndex];
        var correctionAvailable = now.AddMinutes(-30);
        var correctionId = $"synthetic-{original.ExchangeValueDate:yyyyMMdd}-r2";
        observations.Add(Observation(definition, correctionId, original.ExchangeValueDate,
            original.ImpliedVolatility!.Value + 0.0025m,
            original.Provenance.Contributors[0].UnderlyingFuturesContractId, 2, original.ObservationId,
            correctionAvailable));
        events.Add(new(SyntheticVolatilityFixtureEventKind.Correction, correctionAvailable,
            "SYNTHETIC append-only correction; the r1 observation remains available for as-known replay.", correctionId));

        return new(true, "simulation", "Closed", now, definition,
            observations.ToImmutable(), events.OrderBy(x => x.OccurredAtUtc).ToImmutableArray());
    }

    static OptionIvObservation Observation(VolatilitySeriesDefinition definition, string id, DateOnly date,
        decimal iv, string underlying, int revision, string? supersedes, DateTimeOffset available) =>
        new(OptionIvObservation.CurrentSchemaVersion, id, definition.Identity, date, "daily-close", iv,
            VolatilityValueUnit.AnnualDecimal, VolatilityObservationStatus.Qualified, "SYNTHETIC_FIXTURE",
            available.AddMinutes(-2), available.AddMinutes(-1), available, revision, supersedes,
            new([new($"SYNTHETIC-{id}", underlying, available.AddMinutes(-2), available.AddMinutes(-1),
                    revision, revision, Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"))],
                "synthetic-pricer/v1", $"synthetic-input-{id}", "SYNTHETIC-NOT-LIVE"));

    static DateOnly PreviousWeekday(DateOnly date)
    {
        do date = date.AddDays(-1); while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        return date;
    }

    static IEnumerable<DateOnly> PriorWeekdays(DateOnly current, int count)
    {
        var dates = new DateOnly[count];
        var date = current;
        for (var index = count - 1; index >= 0; index--)
        {
            do date = date.AddDays(-1); while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
            dates[index] = date;
        }
        return dates;
    }
}
