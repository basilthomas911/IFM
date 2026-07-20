namespace TomasAI.IFM.Domain.Fund.Shared;

/// <summary>
/// Represents a collection of fund orders that can be accessed by order ID and supports enumeration, addition, and
/// removal of orders.
/// </summary>
/// <remarks>The collection provides methods to check for the existence of an order, add single or multiple
/// orders, and remove orders. Implementations may enforce uniqueness of order IDs and may throw exceptions if duplicate
/// or invalid orders are added. The interface extends <see cref="IEnumerable{IFundOrder}"/>, allowing iteration over
/// all contained fund orders.</remarks>
public interface IFundOrderCollection : IEnumerable<IFundOrder>
{
    int Count { get; }
    IFundOrder this[int orderId] { get; }
    bool Exists(int orderId);
    void Add(IFundOrder item);
    void AddRange(IEnumerable<IFundOrder> items);
    void Remove(IFundOrder item);
}
