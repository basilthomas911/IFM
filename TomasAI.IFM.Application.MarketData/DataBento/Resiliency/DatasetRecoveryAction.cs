using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public enum DatasetRecoveryAction
{
    None = 0,
    CooperativeReset = 1,
    ReplaceProcess = 2,
    StopForClosure = 3
}
