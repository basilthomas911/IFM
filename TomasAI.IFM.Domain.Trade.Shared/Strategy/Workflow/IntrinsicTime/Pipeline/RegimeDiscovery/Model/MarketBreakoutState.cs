namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Identifies a market-structure breakout direction.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketBreakoutState : byte
{
    /// <summary>No breakout is present.</summary>
    None = 0,
    /// <summary>Price is breaking below its rolling low.</summary>
    Down = 1,
    /// <summary>Price is breaking above its rolling high.</summary>
    Up = 2
}
