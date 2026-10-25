namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Identifies the price-structure classification.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketStructureClassification : byte
{
    /// <summary>The structure is incomplete and cannot complete the pipeline.</summary>
    Unknown = 0,
    /// <summary>Price is directionally organized.</summary>
    Trending = 1,
    /// <summary>Price is range bound.</summary>
    Ranging = 2,
    /// <summary>Price and volatility are compressing.</summary>
    Compressing = 3,
    /// <summary>Price and volatility are expanding.</summary>
    Expanding = 4,
    /// <summary>Price is breaking beyond its configured range.</summary>
    BreakingOut = 5,
    /// <summary>Price is transitioning between stable classifications.</summary>
    Transitioning = 6
}
