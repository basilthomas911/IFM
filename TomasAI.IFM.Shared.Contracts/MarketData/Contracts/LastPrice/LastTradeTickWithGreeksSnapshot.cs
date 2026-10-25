namespace TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

/// <summary>
/// Atomically couples the latest option trade with the most recent
/// quote-derived Greeks state available when that trade was processed.
/// </summary>
/// <param name="Tick">The tick.</param>
/// <param name="Greeks">The calculated option sensitivities, when available.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct LastTradeTickWithGreeksSnapshot(
    LastTradeTickSnapshot Tick,
    OptionGreeksSnapshot Greeks);
