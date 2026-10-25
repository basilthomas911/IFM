namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Identifies the calculation area that produced evidence.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum RegimeEvidenceArea : byte
{
    /// <summary>No area is identified.</summary>
    Unknown = 0,
    /// <summary>Configuration evidence.</summary>
    Configuration = 1,
    /// <summary>Market-data quality evidence.</summary>
    Data = 2,
    /// <summary>Trend evidence.</summary>
    Trend = 3,
    /// <summary>Volatility evidence.</summary>
    Volatility = 4,
    /// <summary>Market Structure evidence.</summary>
    MarketStructure = 5,
    /// <summary>Fusion evidence.</summary>
    Fusion = 6,
    /// <summary>Pipeline lifecycle evidence.</summary>
    Pipeline = 7
}
