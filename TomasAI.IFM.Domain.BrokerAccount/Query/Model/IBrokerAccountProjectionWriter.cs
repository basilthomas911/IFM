using Microsoft.Extensions.Hosting;
using MessagePack;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.BrokerAccount.Query.Model;

/// <summary>Persists account data carried by committed domain events.</summary>
public interface IBrokerAccountProjectionWriter
{
    ValueTask ProjectAsync(BrokerAccountDefinition brokerAccountDefinition, CancellationToken cancellationToken = default);
}
