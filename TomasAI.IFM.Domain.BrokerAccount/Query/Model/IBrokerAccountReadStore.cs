using Microsoft.Extensions.Hosting;
using MessagePack;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.BrokerAccount.Query.Model;

/// <summary>Reads persisted account definitions; command state must not populate this service.</summary>
public interface IBrokerAccountReadStore
{
    /// <summary>Reads the PostgreSQL projection for an account used by synchronous broker-order authorization.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1716", Justification = "Get is the established C# synchronous authorization contract; renaming it would change adapter implementations and consumers.")]
    BrokerAccountDefinition? Get(BrokerAccountId accountId);
    /// <summary>Reads the persisted projection asynchronously for query actors.</summary>
    ValueTask<BrokerAccountDefinition?> GetAsync(BrokerAccountId accountId, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Get(accountId));
}
