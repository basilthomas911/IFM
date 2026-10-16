using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Persistence;
/// <summary>Commits command audit, accepted trade source facts and required material rows before any actor handoff.</summary>
internal static class TradeAtomicPersistence
{
    internal static async Task CommitAsync(ICommand command, DomainEventCollection pending, long expectedVersion, IEventSourceActorDbContext source, ITradeDbContext db, IPostgresEventTransaction? transactions, CancellationToken token = default)
    {
        if (!string.Equals(source.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) || !string.Equals(db.ProviderName, "System.Data.Postgres", StringComparison.Ordinal) || PostgresDatabaseIdentity.Parse(source.ConnectionString) != PostgresDatabaseIdentity.Parse(db.ConnectionString))
            throw new InvalidOperationException("TradeDb and its source event log must share the same PostgreSQL database/security identity.");
        var owner = transactions ?? throw new InvalidOperationException("Atomic trade persistence requires the PostgreSQL event transaction service.");
        var writer = db.Database as IEnlistedTradeDbWriter ?? throw new InvalidOperationException("TradeDb does not support enlisted persistence.");
        await owner.ExecuteOnceAsync(async (transaction, ct) =>
        {
            await transaction.ReserveCommandAsync(command, ct).ConfigureAwait(false);
            var version = expectedVersion;
            foreach (var sourceEvent in pending)
            {
                await transaction.AppendAsync(command.StreamId, command.CommandId, sourceEvent, version++, ct).ConfigureAwait(false);
                switch (sourceEvent)
                {
                    case TradeOrderChangedEvent order:
                        await writer.ProjectOrderAsync(transaction, order.TradeOrderDefinition, order.ReceivedOn, ct).ConfigureAwait(false);
                        break;
                    case OrderExecutionChangedEvent execution:
                        await writer.ProjectExecutionAsync(transaction, execution.OrderExecutionDefinition, ct).ConfigureAwait(false);
                        break;
                    case FuturesTradeChangedEvent trade:
                        await writer.ProjectTradeAsync(transaction, trade.EstablishedTradeDefinition, ct).ConfigureAwait(false);
                        break;
                    case OptionTradeChangedEvent trade:
                        await writer.ProjectTradeAsync(transaction, trade.EstablishedTradeDefinition, ct).ConfigureAwait(false);
                        break;
                    case IronCondorMonitoringInitializedEvent limits:
                        if (limits.MonitoringInitialization.ApprovedOrder is { } approval)
                        {
                            var json = await transaction.ScalarAsync(
                                "SELECT definition::text FROM portfolio.accepted_trade_order WHERE portfolio_id=$1 AND fund_id=$2 AND order_id=$3 FOR SHARE;",
                                [approval.Id.PortfolioId, approval.Id.FundId, approval.Id.OrderId], ct).ConfigureAwait(false);
                            var canonical = json is string text ? System.Text.Json.JsonSerializer.Deserialize<TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.PortfolioExecutionOrderInstruction>(text) : null;
                            if (canonical is null || canonical.PortfolioApprovalId != approval.PortfolioApprovalId
                                || canonical.DefinitionHash != approval.DefinitionHash || canonical.Revision != approval.Revision
                                || canonical.RequiredCapital != approval.RequiredCapital || canonical.MaximumLoss != approval.MaximumLoss
                                || !TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared.Serialize(
                                    TomasAI.IFM.Domain.Trade.Shared.Portfolio.PortfolioExecutionContractMapper.ToTradeOrder(canonical).Components).AsSpan().SequenceEqual(
                                    TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared.Serialize(approval.Components)))
                                throw new InvalidOperationException("TRADE_LIMITS.CANONICAL_APPROVAL_MISMATCH");
                            var trade = limits.MonitoringInitialization.IronCondorTrade;
                            if (await transaction.ScalarAsync("SELECT tradeid FROM trade.trade_limit WHERE tradeid=$1 FOR UPDATE;",
                                [trade.Id.TradeId], ct).ConfigureAwait(false) is not null)
                                throw new InvalidOperationException("TRADE_LIMITS.ALREADY_EXISTS;Existing limits must be preserved.");
                            var stored = await transaction.ScalarAsync(
                                "SELECT payload FROM trade.established_trade WHERE portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4 FOR SHARE;",
                                [trade.Id.PortfolioId, trade.Id.FundId, trade.Id.OrderId, trade.Id.TradeId], ct).ConfigureAwait(false);
                            if (stored is not byte[] bytes || !TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared.Serialize(
                                TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared.Deserialize<TomasAI.IFM.Domain.Trade.Shared.EstablishedTradeDefinition>(bytes)).AsSpan()
                                .SequenceEqual(TomasAI.IFM.Framework.Serialization.MessagePackBinarySerializer.Shared.Serialize(trade)))
                                throw new InvalidOperationException("TRADE_LIMITS.CANONICAL_TRADE_MISMATCH");
                        }
                        await writer.ProjectMonitoringLimitsAsync(transaction, limits.TradeLimits, limits.SpreadLimits, ct).ConfigureAwait(false);
                        break;
                    case PositionChangedEvent position:
                        await writer.ProjectPositionAsync(transaction, position.PositionSnapshot, ct).ConfigureAwait(false);
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported atomic trade source event: " + sourceEvent.GetType().Name);
                }
            }

            return true;
        }, token).ConfigureAwait(false);
    }
}
