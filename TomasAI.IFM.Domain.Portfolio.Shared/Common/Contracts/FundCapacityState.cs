namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

public enum FundCapacityState
{
    Unknown = 0,
    Available = 1,
    Constrained = 2,
    Blocked = 3,
    ReduceOnly = 4,
}
