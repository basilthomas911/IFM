namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Identifies the signed market direction produced by Regime Discovery.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum RegimeDirection : byte
{
    /// <summary>No direction has been calculated.</summary>
    Unknown = 0,
    /// <summary>The market direction is negative.</summary>
    Down = 1,
    /// <summary>The market direction is neutral.</summary>
    Neutral = 2,
    /// <summary>The market direction is positive.</summary>
    Up = 3
}
