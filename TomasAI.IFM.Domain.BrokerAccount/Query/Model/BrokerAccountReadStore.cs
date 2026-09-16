using TomasAI.IFM.Domain.BrokerAccount.Contracts;

namespace TomasAI.IFM.Domain.BrokerAccount.Query.Model;

/// <summary>Read projection for the latest immutable account state.</summary>
public interface IBrokerAccountReadStore
{
    /// <summary>Publishes a committed account state atomically.</summary>
    void Set(BrokerAccountDefinition value);

    /// <summary>Reads the current state for the exact account.</summary>
    BrokerAccountDefinition? Get(BrokerAccountId id);
}

/// <summary>Lock-free singleton broker-account read projection.</summary>
public sealed class BrokerAccountReadStore : IBrokerAccountReadStore
{
    private BrokerAccountDefinition? _current;

    /// <inheritdoc />
    public void Set(BrokerAccountDefinition value) => Volatile.Write(ref _current, value);

    /// <inheritdoc />
    public BrokerAccountDefinition? Get(BrokerAccountId id)
    {
        var value = Volatile.Read(ref _current);
        return value?.Id == id ? value : null;
    }
}
