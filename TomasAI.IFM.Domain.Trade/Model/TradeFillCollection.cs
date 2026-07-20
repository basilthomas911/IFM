using System.Collections;

namespace TomasAI.IFM.Domain.Trade.Model;

public class TradeFillCollection() : ITradeFillCollection
{
    readonly List<ITradeFill> _tradeFills = [];

    public int Count => _tradeFills.Count;
    
    public void Add(ITradeFill item) => _tradeFills.Add(item);

    public void Add(ICollection<ITradeFill> items) => _tradeFills.AddRange(items);

    public void Clear() => _tradeFills.Clear();

    public IEnumerator<ITradeFill> GetEnumerator() => _tradeFills.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_tradeFills).GetEnumerator();

}
