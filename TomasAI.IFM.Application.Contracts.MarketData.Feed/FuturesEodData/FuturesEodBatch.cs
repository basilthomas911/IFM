using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared;

/// <summary>Qualifies canonical futures EOD projection keys before issuing the batch write.</summary>
public sealed class FuturesEodBatch
{
    public FuturesEodBatch(IEnumerable<FuturesEodDataV2ReadModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var materialized = rows.ToArray();
        var keys = new HashSet<(string ContractId, DateOnly ValueDate, string Symbol)>(materialized.Length);
        foreach (var row in materialized)
            if (!keys.Add((row.ContractId, row.ValueDate, row.Symbol)))
                throw new ArgumentException($"The futures-EOD write contains duplicate canonical key ('{row.ContractId}', '{row.ValueDate:yyyy-MM-dd}', '{row.Symbol}').", nameof(rows));
        Rows = Array.AsReadOnly(materialized);
    }

    public IReadOnlyList<FuturesEodDataV2ReadModel> Rows { get; }
}
