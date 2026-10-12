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
