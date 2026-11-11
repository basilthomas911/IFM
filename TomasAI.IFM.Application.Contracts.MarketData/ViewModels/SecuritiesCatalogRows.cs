namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>Canonical persisted Securities rows captured under the caller's maintenance transaction.</summary>
/// <param name="Futures">The complete persisted futures definitions.</param>
/// <param name="Options">The complete persisted option definitions.</param>
/// <param name="RolloverPointers">The persisted rollover assignments, including unassigned roots.</param>
public sealed record SecuritiesCatalogRows(FuturesContractV3ReadModel[] Futures,
    FuturesOptionContractReadModel[] Options, FuturesContractRolloverReadModel[] RolloverPointers);
