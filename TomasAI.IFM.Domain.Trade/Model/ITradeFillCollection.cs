namespace TomasAI.IFM.Domain.Trade.Model;

public interface ITradeFillCollection : IEnumerable<ITradeFill>
{
    int Count { get; }
    void Add(ITradeFill item);
    void Clear();
}
