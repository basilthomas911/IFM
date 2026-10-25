namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Identifies the absolute strength of a directional trend.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum TrendRegimeStrength : byte
{
    /// <summary>No directional strength applies.</summary>
    None = 0,
    /// <summary>The trend is weak.</summary>
    Weak = 1,
    /// <summary>The trend is moderate.</summary>
    Moderate = 2,
    /// <summary>The trend is strong.</summary>
    Strong = 3,
    /// <summary>The trend is extreme.</summary>
    Extreme = 4
}
