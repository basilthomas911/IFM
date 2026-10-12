namespace TomasAI.IFM.Domain.Portfolio.Operations;

public enum PortfolioOperation
{
    Read = 0,
    AdministerPortfolio = 1,
    AdministerFund = 2,
    DelegateAllocation = 3,
    DelegateRiskEnvelope = 4,
    AssignTemplate = 5,
    ReserveComposition = 6,
    RecordCompositionResult = 7,
    RecordRiskResult = 8,
}
