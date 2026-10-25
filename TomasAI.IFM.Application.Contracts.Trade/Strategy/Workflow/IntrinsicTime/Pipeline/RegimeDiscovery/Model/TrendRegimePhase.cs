namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Identifies the current trend lifecycle phase.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum TrendRegimePhase : byte
{
    /// <summary>No phase has been calculated.</summary>
    Unknown = 0,
    /// <summary>The market is range bound.</summary>
    RangeBound = 1,
    /// <summary>A directional trend is emerging.</summary>
    Emerging = 2,
    /// <summary>A directional trend is established.</summary>
    Established = 3,
    /// <summary>The trend is losing momentum.</summary>
    Exhausting = 4,
    /// <summary>The trend is reversing.</summary>
    Reversing = 5
}
