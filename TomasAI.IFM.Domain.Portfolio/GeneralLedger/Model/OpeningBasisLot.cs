namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>One opening execution lot within one leg of one strategy position.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly record struct OpeningBasisLot(decimal SignedQuantity, decimal Price, decimal CashMultiplier);
