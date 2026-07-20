namespace TomasAI.IFM.Shared.OptionPricer;

public interface ISpreadDistributionJobCollection : IEnumerable<ISpreadDistributionJob>
{
    ISpreadDistributionJob? this[int jobId] { get; }
    int Count { get; }
    void Add(ISpreadDistributionJob optionPricer);
    bool Exists(int jobId);
}
