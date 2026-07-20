namespace TomasAI.IFM.Domain.Trade.Model;

public interface IOptionLegDataCollection : IEnumerable<IOptionLegData>
{
    int Count { get; }
    double TradeMultiplier { get; }
    IOptionLegData? this[string contractId] { get; }
    bool Exists(string contractId);
    void Add(IOptionLegData item);
    void Clear();
    void Remove(string contractId);
    decimal GetNetSpread();
    decimal GetTradeValue();
    double GetOTMProbability();
}
