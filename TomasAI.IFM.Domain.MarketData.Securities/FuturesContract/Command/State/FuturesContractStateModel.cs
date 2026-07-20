using TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.Model;
using TomasAI.IFM.Shared.MarketData;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesContract.Command.State;

/// <summary>
/// Represents a model for managing a collection of futures contracts, allowing operations such as adding, removing, and
/// checking for the existence of contracts.
/// </summary>
/// <remarks>This class provides methods to manage futures contracts identified by unique IDs. It supports adding
/// new contracts, removing existing ones, and checking if a contract exists in the collection.</remarks>
internal class FuturesContractStateModel
{
    readonly Dictionary<FuturesContractId, FuturesSecuritiesContract>? _futuresContractMap = [];
    
    /// <summary>
    /// Determines whether the specified futures contract ID exists in the collection.
    /// </summary>
    /// <param name="id">The <see cref="FuturesContractId"/> to locate in the collection.</param>
    /// <returns><see langword="true"/> if the collection contains an entry with the specified ID; otherwise, <see
    /// langword="false"/>.</returns>
    public bool ContainsKey(FuturesContractId id)
        => _futuresContractMap?.ContainsKey(id) ?? false;

    /// <summary>
    /// Removes the specified futures contract from the collection.
    /// </summary>
    /// <param name="id">The identifier of the futures contract to remove.</param>
    /// <returns><see langword="true"/> if the futures contract was successfully removed; otherwise, <see langword="false"/>.
    /// Returns <see langword="false"/> if the collection is <see langword="null"/> or the contract does not exist.</returns>
    public bool Remove(FuturesContractId id)
        => _futuresContractMap?.Remove(id) ?? false;

    /// <summary>
    /// Adds a futures contract to the collection, associating it with the specified identifier.
    /// </summary>
    /// <remarks>If a contract with the same identifier already exists in the collection, an exception may be
    /// thrown. Ensure that the identifier is unique before calling this method.</remarks>
    /// <param name="id">The unique identifier of the futures contract to add. Cannot be <see langword="null"/>.</param>
    /// <param name="contract">The futures contract to associate with the specified identifier. Cannot be <see langword="null"/>.</param>
    public void Add(FuturesContractId id, FuturesSecuritiesContract contract)
        => _futuresContractMap?.Add(id, contract);
}
