namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Identifies how volatility is changing.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum VolatilityRegimeChange : byte
{
    /// <summary>No change has been calculated.</summary>
    Unknown = 0,
    /// <summary>Volatility is contracting.</summary>
    Contracting = 1,
    /// <summary>Volatility is stable.</summary>
    Stable = 2,
    /// <summary>Volatility is expanding.</summary>
    Expanding = 3
}
