namespace TomasAI.IFM.Domain.Trade.Order.Broker.Model;
/// <summary>Constraint-produced set of actions a policy may select.</summary>
[Flags]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The existing broker policy mask uses ushort bits; preserve its published representation.")]
public enum MicroExecutionActionMask : ushort
{
    None = 0,
    Wait = 1 << 0,
    Place = 1 << 1,
    UpdateLimit = 1 << 2,
    Cancel = 1 << 3,
    Reconcile = 1 << 4,
    Escalate = 1 << 5
}
