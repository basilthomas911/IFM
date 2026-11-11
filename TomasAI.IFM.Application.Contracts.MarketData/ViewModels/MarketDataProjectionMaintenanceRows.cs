namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>Physical count and fingerprint of a selected canonical table or index.</summary>
public sealed record MarketDataProjectionInventoryRow(string Kind, long Count, string Fingerprint);

/// <summary>Exact rows observed before and after copying the Treasury lookup indexes.</summary>
public sealed record YieldCurveLookupInventory(IReadOnlyList<YieldCurveRateReadModel> Canonical,
    IReadOnlyList<YieldCurveRateReadModel> DateIndex, IReadOnlyList<int> YearIndex);

/// <summary>Prepared raw-signal lookup row accepted by the owning repair operation.</summary>
public sealed record FuturesTradeSignalLookupRow(string Key, string Scope, long SequenceId, string ContractId,
    DateOnly ValueDate, string TimePeriod);

/// <summary>Prepared preservation of a malformed legacy signal; the canonical source remains intact.</summary>
public sealed record FuturesTradeSignalQuarantineRow(string Fingerprint, string Payload, string Reason, DateTime ObservedAtUtc);
