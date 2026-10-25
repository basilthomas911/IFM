namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Identifies the composite volatility level.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum VolatilityRegimeLevel : byte
{
    /// <summary>No level has been calculated.</summary>
    Unknown = 0,
    /// <summary>Volatility is low.</summary>
    Low = 1,
    /// <summary>Volatility is normal.</summary>
    Normal = 2,
    /// <summary>Volatility is high.</summary>
    High = 3,
    /// <summary>Volatility is extreme.</summary>
    Extreme = 4
}
