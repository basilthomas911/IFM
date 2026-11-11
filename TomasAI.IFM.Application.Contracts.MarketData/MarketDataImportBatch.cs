using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>Caller-qualified rows and duplicate handling for a single accepted import.</summary>
public sealed class MarketDataImportBatch<T>
{
    internal MarketDataImportBatch(T[] rows, ImportDuplicatePolicy policy, Guid commandId)
        => (Rows, Policy, CommandId) = (Array.AsReadOnly(rows), policy, commandId);

    public IReadOnlyList<T> Rows { get; }
    public ImportDuplicatePolicy Policy { get; }
    public Guid CommandId { get; }
}

public static class MarketDataImportBatch
{
    public static MarketDataImportBatch<EconomicCalendarReadModel> Single(EconomicCalendarReadModel row)
        => new([row], ImportDuplicatePolicy.Overwrite, Guid.Empty);

    public static MarketDataImportBatch<YieldCurveRateReadModel> Single(YieldCurveRateReadModel row)
        => new([row], ImportDuplicatePolicy.Overwrite, Guid.Empty);

    public static MarketDataImportBatch<EconomicCalendarReadModel> Calendars(
        IReadOnlyList<EconomicCalendarReadModel> rows, ImportDuplicatePolicy policy = ImportDuplicatePolicy.Overwrite,
        Guid commandId = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        RequirePolicy(policy, commandId);
        var ordered = rows.OrderBy(row => row.CountryCode, StringComparer.Ordinal)
            .ThenBy(row => row.EventDate).ThenBy(row => row.EventName, StringComparer.Ordinal).ToArray();
        var keys = new HashSet<(string, DateTime, string)>();
        foreach (var row in ordered)
            if (!keys.Add((row.CountryCode, NormalizeCalendarTimestamp(row.EventDate), row.EventName)))
                throw new ArgumentException("An import batch contains duplicate economic-calendar identities.", nameof(rows));
        return new(ordered, policy, commandId);
    }

    public static MarketDataImportBatch<YieldCurveRateReadModel> YieldCurves(
        IReadOnlyList<YieldCurveRateReadModel> rows, ImportDuplicatePolicy policy = ImportDuplicatePolicy.Overwrite,
        Guid commandId = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        RequirePolicy(policy, commandId);
        var ordered = rows.OrderBy(row => row.ValueDate).ToArray();
        for (var index = 1; index < ordered.Length; index++)
            if (ordered[index - 1].ValueDate == ordered[index].ValueDate)
                throw new ArgumentException("An import batch must contain one Treasury curve per value date.", nameof(rows));
        return new(ordered, policy, commandId);
    }

    public static DateTime NormalizeCalendarTimestamp(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return new DateTime(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerMillisecond), DateTimeKind.Utc);
    }

    static void RequirePolicy(ImportDuplicatePolicy policy, Guid commandId)
    {
        if (!Enum.IsDefined(policy))
            throw new ArgumentOutOfRangeException(nameof(policy));
        if (policy == ImportDuplicatePolicy.Reject && commandId == Guid.Empty)
            throw new ArgumentException("Reject imports require a durable command identity.", nameof(commandId));
    }
}
