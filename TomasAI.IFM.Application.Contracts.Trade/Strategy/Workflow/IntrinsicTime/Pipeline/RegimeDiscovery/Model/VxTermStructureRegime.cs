namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;

/// <summary>Identifies the VX futures term-structure state.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum VxTermStructureRegime : byte
{
    /// <summary>No term-structure state has been calculated.</summary>
    Unknown = 0,
    /// <summary>The front contract is below the second contract.</summary>
    Contango = 1,
    /// <summary>The two contracts are effectively flat.</summary>
    Flat = 2,
    /// <summary>The front contract is above the second contract.</summary>
    Backwardation = 3
}
