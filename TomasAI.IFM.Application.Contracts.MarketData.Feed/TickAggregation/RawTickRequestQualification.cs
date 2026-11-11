using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

/// <summary>Qualifies raw tick reads and caller-prepared write batches before invoking persistence.</summary>
public static class RawTickRequestQualification
{
    /// <summary>Requires an exact raw-tick partition identity.</summary>
    public static void RequireScope(string contractId, DateOnly valueDate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contractId);
        if (valueDate == default) throw new ArgumentException("A tick value date is required.", nameof(valueDate));
    }

    /// <summary>Requires an ordered clustering-time interval within the selected partition.</summary>
    public static void RequireWindow(string contractId, DateOnly valueDate, TimeOnly fromUtc, TimeOnly throughUtc)
    {
        RequireScope(contractId, valueDate);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fromUtc, throughUtc);
    }

    /// <summary>Qualifies unique futures tick identities before writing a caller's batch.</summary>
    public static void RequireDistinctFuturesRows(ICollection<FuturesTickDataV2ReadModel> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            RequireScope(value.ContractId, value.ValueDate);
        }
        if (values.Select(row => (row.ContractId, row.ValueDate, row.TickId)).Distinct().Count() != values.Count)
            throw new ArgumentException("Duplicate raw tick identities in one batch.", nameof(values));
    }

    /// <summary>Qualifies unique option tick identities before writing a caller's batch.</summary>
    public static void RequireDistinctOptionRows(ICollection<FuturesOptionTickDataV2ReadModel> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            RequireScope(value.ContractId, value.ValueDate);
        }
        if (values.Select(row => (row.ContractId, row.ValueDate, row.TickId)).Distinct().Count() != values.Count)
            throw new ArgumentException("Duplicate raw tick identities in one batch.", nameof(values));
    }
}
