using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer;

public interface ISpreadDistributionJob
{
    int JobId { get; }
    int OrderId { get; }
    int TradeId { get; }
    TradeType TradeType { get; }
    TradeStatus TradeStatus { get; }
    DateOnly ValueDate { get; }
    int DaysToExpiry { get; }
    OptionStyle OptionStyle { get; }
    OptionType OptionType { get; }
    DateTime JobSubmitted { get; }
    string JobStatus { get; }
    DateTime? JobCompleted { get; }
    DateTime? JobFailed { get; }
    double LossProbabilityFactor { get; }

    ISpreadDistributionJob SetJobStatus(string jobStatus);
    ISpreadDistributionJob SetJobCompleted(DateTime jobCompleted);
    ISpreadDistributionJob SetJobFailed(DateTime jobFailed);

}
