using TomasAI.IFM.Domain.BrokerAccount.Contracts;

namespace TomasAI.IFM.Domain.BrokerAccount.Query.Model;

/// <summary>Persists account data carried by committed domain events.</summary>
public interface IBrokerAccountProjectionWriter
{
    /// <summary>Upserts the projection for the supplied account and revision.</summary>
    /// <param name="brokerAccountDefinition">The account revision to project.</param>
    /// <param name="cancellationToken">Cancels the projection write.</param>
    /// <returns>Completion of the projection write.</returns>
    ValueTask ProjectAsync(BrokerAccountDefinition brokerAccountDefinition, CancellationToken cancellationToken = default);
}
