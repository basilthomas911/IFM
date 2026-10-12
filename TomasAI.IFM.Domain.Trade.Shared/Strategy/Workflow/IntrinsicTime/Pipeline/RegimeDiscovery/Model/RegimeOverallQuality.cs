namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Identifies the final Regime Discovery result quality.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum RegimeOverallQuality : byte
{
    /// <summary>No valid quality has been calculated.</summary>
    Unknown = 0,
    /// <summary>The result has low confidence.</summary>
    Low = 1,
    /// <summary>The result is usable with degraded evidence.</summary>
    Degraded = 2,
    /// <summary>The result is acceptable.</summary>
    Acceptable = 3,
    /// <summary>The result has high-quality evidence.</summary>
    High = 4
}
