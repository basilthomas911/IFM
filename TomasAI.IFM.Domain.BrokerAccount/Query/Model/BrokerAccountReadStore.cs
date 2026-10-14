using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;
using Microsoft.Extensions.Hosting;
using MessagePack;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.BrokerAccount.Query.Model;

/// <summary>PostgreSQL account read-model repository; highest projected revision is current.</summary>
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
        var account = await Use("BrokerAccount.Get", "SELECT account_definition FROM trade.broker_account_read_model WHERE account_alias = $1 ORDER BY revision DESC LIMIT 1;")
            .SetParameters((object)Values(Text(accountId.AccountAlias)))
            .ExecuteSingleAsync(row => MessagePackSerializer.Deserialize<BrokerAccountDefinition>(row.GetBytes(0)), cancellationToken).ConfigureAwait(false);
        return account is null ? null : BrokerAccountTradingQualifications.TradingView(account,
            hostEnvironment?.IsDevelopment() == true);
    }
    /// <inheritdoc />
    public async ValueTask ProjectAsync(BrokerAccountDefinition brokerAccountDefinition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(brokerAccountDefinition);
        await Use("BrokerAccount.Project", "INSERT INTO trade.broker_account_read_model (account_alias, revision, account_definition) VALUES ($1, $2, $3) ON CONFLICT (account_alias, revision) DO UPDATE SET account_definition = EXCLUDED.account_definition;")
            .SetParameters((object)Values(Text(brokerAccountDefinition.Id.AccountAlias), Bigint((long)brokerAccountDefinition.Revision), Bytea(MessagePackSerializer.Serialize(brokerAccountDefinition with { DevelopmentQualificationsExempt = false }, cancellationToken: cancellationToken))))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }
}
