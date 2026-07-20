using TomasAI.IFM.Shared.MarketData;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;

/// <summary>
/// Represents a model for managing a collection of futures option contracts,  providing operations to add, remove, and
/// query contracts by their unique identifiers.
/// </summary>
/// <remarks>This class maintains a mapping between unique contract identifiers and their associated futures
/// option contracts. It supports operations to check for the existence of a contract, add new contracts, and remove
/// existing ones. If the internal collection is <see langword="null"/>, all operations will behave as no-ops or return
/// default values.</remarks>
internal class FuturesOptionContractModel
{
    readonly Dictionary<string, FuturesOptionSecuritiesContract>? _futuresOptionContractMap = [];

    /// <summary>
    /// Determines whether the specified <see cref="FuturesOptionContractId"/> exists in the collection.
    /// </summary>
    /// <param name="id">The <see cref="FuturesOptionContractId"/> to locate in the collection.</param>
    /// <returns><see langword="true"/> if the specified <see cref="FuturesOptionContractId"/> exists in the collection;
    /// otherwise, <see langword="false"/>.</returns>
    public bool ContainsKey(string id)
        => _futuresOptionContractMap?.ContainsKey(id) ?? false;

    /// <summary>
    /// Removes the specified futures option contract from the collection.
    /// </summary>
    /// <param name="id">The identifier of the futures option contract to remove.</param>
    /// <returns><see langword="true"/> if the contract was successfully removed; otherwise, <see langword="false"/>. Returns
    /// <see langword="false"/> if the collection is <see langword="null"/> or the contract does not exist.</returns>
    public bool Remove(string id)
        => _futuresOptionContractMap?.Remove(id) ?? false;

    /// <summary>
    /// Adds a futures option contract to the collection, associating it with the specified contract identifier.
    /// </summary>
    /// <remarks>If the specified identifier already exists in the collection, the associated contract will be
    /// replaced with the new one.</remarks>
    /// <param name="id">The unique identifier of the futures option contract to add. Cannot be <see langword="null"/>.</param>
    /// <param name="contract">The futures option contract to associate with the specified identifier. Cannot be <see langword="null"/>.</param>
    public void Add(string id, FuturesOptionSecuritiesContract contract)
        => _futuresOptionContractMap?.Add(id, contract);
}
