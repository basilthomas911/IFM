namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1700", Justification = "This is a real persisted financial lifecycle state; retain its public name and numeric value.")]
public enum FundCompositionState
{
    Unknown = 0,
    Draft = 1,
    IdentityReserved = 2,
    TemplateSelected = 3,
    Composing = 4,
    Composed = 5,
    CompositionFailed = 6,
    RiskPending = 7,
    RiskRejected = 8,
    RiskApproved = 9,
    Cancelled = 10,
    Expired = 11,
    ExecutionRequested = 12,
    Executing = 13,
    Executed = 14,
    ExecutionFailed = 15,
}
