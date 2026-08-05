using System.Collections;
using TomasAI.IFM.Domain.Fund.Shared;

namespace TomasAI.IFM.Domain.Fund.Command.Model;

/// <summary>
/// fund order collection
/// </summary>
public class FundOrderCollection : IFundOrderCollection
{
    readonly Dictionary<int, IFundOrder> _fundOrders;

    /// <summary>
    /// fund order collection constructor
    /// </summary>
    public FundOrderCollection() => _fundOrders = [];

    /// <summary>
    /// return count of fund order in collection
    /// </summary>
    public int Count => _fundOrders.Count;

    /// <summary>
    /// return selected fund order or null if noex exists
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public IFundOrder this[int orderId] => _fundOrders.TryGetValue(orderId, out var order)
        ? order
        : throw new KeyNotFoundException($"Fund order with orderId {orderId} was not found.");

    public bool Exists(int orderId) => _fundOrders.ContainsKey(orderId);

    public void Add(IFundOrder item) => _fundOrders[item.OrderId] = item;

    public void AddRange(IEnumerable<IFundOrder> items)
    {
        foreach (var item in items)
            Add(item);
    }

    public void Remove(IFundOrder item) => _fundOrders.Remove(item.OrderId);

    public IEnumerator<IFundOrder> GetEnumerator() => _fundOrders.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

}
