using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;

namespace TomasAI.IFM.Application.EventSourcing.Projections.BrokerAccount;

/// <summary>Applies the host's trading qualifications to projected broker accounts.</summary>
public sealed class BrokerAccountProjectionService(ITradeDbReadContext reader, ITradeDbWriteContext writer, bool isDevelopment)
    : IBrokerAccountReadStore, IBrokerAccountProjectionWriter
{
    /// <inheritdoc/>
    public BrokerAccountDefinition? Get(BrokerAccountId accountId)
        => GetAsync(accountId).AsTask().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async ValueTask<BrokerAccountDefinition?> GetAsync(BrokerAccountId accountId, CancellationToken cancellationToken = default)
    {
        var account = await reader.GetBrokerAccountAsync(accountId, cancellationToken).ConfigureAwait(false);
        return account is null ? null : BrokerAccountTradingQualifications.TradingView(account, isDevelopment);
    }

    /// <inheritdoc/>
    public ValueTask ProjectAsync(BrokerAccountDefinition brokerAccountDefinition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(brokerAccountDefinition);
        return writer.ProjectAsync(brokerAccountDefinition with { DevelopmentQualificationsExempt = false }, cancellationToken);
    }
}
