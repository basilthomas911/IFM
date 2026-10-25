namespace TomasAI.IFM.Shared.MarketData.Contracts.LastPrice;

/// <summary>
/// Atomically couples the latest option quote with the calculation produced
/// for that exact quote observation.
/// </summary>
/// <param name="Tick">The tick.</param>
/// <param name="Greeks">The calculated option sensitivities, when available.</param>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct LastQuoteTickWithGreeksSnapshot(
    LastQuoteTickSnapshot Tick,
    OptionGreeksSnapshot Greeks);
