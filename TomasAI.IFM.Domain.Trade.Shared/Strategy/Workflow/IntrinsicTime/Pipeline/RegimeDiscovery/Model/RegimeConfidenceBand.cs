namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Identifies a normalized confidence band.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum RegimeConfidenceBand : byte
{
    /// <summary>No confidence band has been calculated.</summary>
    Unknown = 0,
    /// <summary>Confidence is below 0.35.</summary>
    Low = 1,
    /// <summary>Confidence is at least 0.35 and below 0.60.</summary>
    Moderate = 2,
    /// <summary>Confidence is at least 0.60 and below 0.80.</summary>
    High = 3,
    /// <summary>Confidence is at least 0.80.</summary>
    VeryHigh = 4
}
