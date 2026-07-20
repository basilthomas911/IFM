using System.Collections;
using TomasAI.IFM.Domain.Fund.Shared;

namespace TomasAI.IFM.Domain.Fund.Command.Model;

/// <summary>
/// fund order collection
/// </summary>
public class FundOrderCollection : IFundOrderCollection
{
    readonly List<IFundOrder> _fundOrders;

    /// <summary>
    /// fund order collection constructor
    /// </summary>
    public FundOrderCollection() => _fundOrders = new List<IFundOrder>();

    /// <summary>
    /// return count of fund order in collection
    /// </summary>
    public int Count => _fundOrders.Count;

    /// <summary>
    /// return selected fund order or null if noex exists
    /// </summary>
    /// <param name="orderId"></param>
    /// <returns></returns>
    public IFundOrder this[int orderId] => _fundOrders.SingleOrDefault(e => e.OrderId == orderId)
        ?? throw new KeyNotFoundException($"Fund order with orderId {orderId} was not found.");

    public bool Exists(int orderId) => _fundOrders.Exists(e => e.OrderId == orderId);

    public void Add(IFundOrder item) => _fundOrders.Add(item);

    public void AddRange(IEnumerable<IFundOrder> items) => _fundOrders.AddRange(items);

    public void Remove(IFundOrder item) => _fundOrders.Remove(item);

    public IEnumerator<IFundOrder> GetEnumerator() => _fundOrders.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_fundOrders).GetEnumerator();

}
