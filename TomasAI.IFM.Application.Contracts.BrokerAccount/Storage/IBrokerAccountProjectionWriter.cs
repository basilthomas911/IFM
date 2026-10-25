using TomasAI.IFM.Domain.BrokerAccount.Contracts;

namespace TomasAI.IFM.Domain.BrokerAccount.Query.Model;

/// <summary>Persists account data carried by committed domain events.</summary>
public interface IBrokerAccountProjectionWriter
{
    ValueTask ProjectAsync(BrokerAccountDefinition brokerAccountDefinition, CancellationToken cancellationToken = default);
}
