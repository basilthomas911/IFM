namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Identifies a deterministic restriction for later strategy stages.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum RegimeRestriction : byte
{
    /// <summary>No restriction applies.</summary>
    None = 0,
    /// <summary>No new trade should be selected.</summary>
    NoNewTrade = 1,
    /// <summary>Trend and structure directions conflict.</summary>
    DirectionConflict = 2,
    /// <summary>The fused confidence is low.</summary>
    LowConfidence = 3,
    /// <summary>Market structure is transitioning.</summary>
    Transition = 4
}
