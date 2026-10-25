namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Identifies one MDI threshold for a trade strategy type.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S101", Justification = "Preserve this existing persisted CLR type identity and acronym spelling.")]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct FuturesMDIId(TradeType TradeType, int MDI);
