namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>Signed opening cost allocated to this close and the remaining position.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public readonly record struct ClosingBasisAllocation(decimal AllocatedSignedBasis, decimal RemainingSignedQuantity,
    decimal RemainingSignedBasis, decimal CumulativeClosedQuantity, decimal CumulativeAllocatedSignedBasis);
