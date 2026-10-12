namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Identifies the severity of a stable Regime Discovery reason.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum RegimeReasonSeverity : byte
{
    /// <summary>No severity applies.</summary>
    None = 0,
    /// <summary>The reason is informational.</summary>
    Information = 1,
    /// <summary>The reason is a warning.</summary>
    Warning = 2,
    /// <summary>The reason restricts later processing.</summary>
    Restriction = 3,
    /// <summary>The reason prevents successful completion.</summary>
    Failure = 4
}
