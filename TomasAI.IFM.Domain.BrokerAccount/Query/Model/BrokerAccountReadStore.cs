using Microsoft.Extensions.Hosting;
using MessagePack;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.BrokerAccount.Query.Model;

/// <summary>ScyllaDB account read-model repository; highest projected revision is current.</summary>
public sealed class BrokerAccountReadStore(IDbConnectionSettings settings, ILogger<DbProvider> logger, IHostEnvironment? hostEnvironment = null)
    : ObjectDataRepository<BrokerAccountReadStore>(settings["TradeDbConnection"], logger), IBrokerAccountReadStore, IBrokerAccountProjectionWriter
{
    /// <inheritdoc />
    public override BrokerAccountReadStore Database => this;
    /// <inheritdoc />
    public BrokerAccountDefinition? Get(BrokerAccountId accountId) => GetAsync(accountId).AsTask().GetAwaiter().GetResult();
    /// <inheritdoc />
    public async ValueTask<BrokerAccountDefinition?> GetAsync(BrokerAccountId accountId, CancellationToken cancellationToken = default)
    {
        var account = await Use("BrokerAccount.Get", "SELECT account_definition FROM broker_account_read_model WHERE account_alias = :AccountAlias LIMIT 1;")
            .SetParameters(new { AccountAlias = accountId.AccountAlias })
            .ExecuteSingleAsync(row => MessagePackSerializer.Deserialize<BrokerAccountDefinition>(row.GetBytes(0)), cancellationToken).ConfigureAwait(false);
        return account is null ? null : BrokerAccountTradingQualifications.TradingView(account,
            hostEnvironment?.IsDevelopment() == true);
    }
    /// <inheritdoc />
    public async ValueTask ProjectAsync(BrokerAccountDefinition brokerAccountDefinition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(brokerAccountDefinition);
        await Use("BrokerAccount.Project", "INSERT INTO broker_account_read_model (account_alias, revision, account_definition) VALUES (:AccountAlias, :Revision, :AccountDefinition);")
            .SetParameters(new { AccountAlias = brokerAccountDefinition.Id.AccountAlias, Revision = (long)brokerAccountDefinition.Revision, AccountDefinition = MessagePackSerializer.Serialize(brokerAccountDefinition with { DevelopmentQualificationsExempt = false }, cancellationToken: cancellationToken) })
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }
}
