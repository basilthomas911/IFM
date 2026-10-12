namespace TomasAI.IFM.Framework.TradeBroker.Contracts;

/// <summary>A local dispatch receipt, never an acknowledgement or execution.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized message/configuration contract, including enum widths, numeric values, and collection shape.")]
public enum FrameworkDispatchOutcome : byte
{
    RejectedLocally = 0,
    AcceptedForDispatch = 1,
    OutcomeUnknown = 2
}
