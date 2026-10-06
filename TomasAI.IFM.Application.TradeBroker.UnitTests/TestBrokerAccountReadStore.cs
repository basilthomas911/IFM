using TomasAI.IFM.Domain.BrokerAccount.Contracts;
namespace TomasAI.IFM.Domain.BrokerAccount.Query.Model;
/// <summary>Lock-free singleton broker-account read projection.</summary>
internal sealed class TestBrokerAccountReadStore : IBrokerAccountReadStore
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
