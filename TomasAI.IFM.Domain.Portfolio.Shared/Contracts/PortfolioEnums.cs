namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

public enum PortfolioOperatingState
{
    Unknown = 0,
    Draft = 1,
    Active = 2,
    Paused = 3,
    ReduceOnly = 4,
    Disabled = 5,
    Retired = 6,
}

public enum FundOperatingState
{
    Unknown = 0,
    Draft = 1,
    Active = 2,
    Paused = 3,
    Disabled = 4,
    Retired = 5,
}

public enum FundCapacityState
{
    Unknown = 0,
    Available = 1,
    Constrained = 2,
    Blocked = 3,
    ReduceOnly = 4,
}

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

public enum CompositionOrigin
{
    Unknown = 0,
    StrategyWorkflow = 1,
    ManualUi = 2,
    ApprovedImport = 3,
}
