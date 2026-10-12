namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>Absolute quantities immediately before and during this close, for one position leg.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly record struct PositionLegCloseQuantity(decimal RemainingBeforeClose, decimal ClosingQuantity);
