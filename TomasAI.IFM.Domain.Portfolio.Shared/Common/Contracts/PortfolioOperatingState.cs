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
