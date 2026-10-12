namespace TomasAI.IFM.Domain.Trade.Order.Broker.Model;
/// <summary>Actions available to the generic broker micro-execution policy.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The existing broker action representation is a byte and is consumed by its action-mask protocol.")]
public enum MicroExecutionAction : byte
{
    Unknown = 0,
    Wait = 1,
    Place = 2,
    UpdateLimit = 3,
    Cancel = 4,
    Reconcile = 5,
    Escalate = 6
}
