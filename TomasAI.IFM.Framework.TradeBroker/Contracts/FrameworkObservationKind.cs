namespace TomasAI.IFM.Framework.TradeBroker.Contracts;

/// <summary>The normalized critical observation type produced by a broker port.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized message/configuration contract, including enum widths, numeric values, and collection shape.")]
public enum FrameworkObservationKind : byte
{
    Unknown = 0,
    Acknowledged = 1,
    Execution = 2,
    Commission = 3,
    Rejected = 4,
    Cancelled = 5,
    AccountSnapshot = 6,
    GateChanged = 7,
    ConnectionChanged = 8,
    OrderCompleted = 9
}
