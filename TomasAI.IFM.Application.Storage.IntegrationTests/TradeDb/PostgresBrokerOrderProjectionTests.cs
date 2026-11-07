using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Domain.Trade.Order.Broker.Query;
using TomasAI.IFM.Domain.Trade.Order.Broker.Query.Actor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Query.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Broker;
using NSubstitute;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresBrokerOrderProjectionTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Rebuild_restores_latest_pending_and_filled_components_without_dispatch()
    {
        var trade = new TradeOrderId(101, 701, 92001);
        var execution = new OrderExecutionId(trade, Guid.NewGuid());
        var pending = new BrokerOrderId(execution, Guid.NewGuid());
        var filled = new BrokerOrderId(execution, Guid.NewGuid());
        var unrelated = new BrokerOrderId(new(new TradeOrderId(101, 702, trade.OrderId), Guid.NewGuid()), Guid.NewGuid());
        await SaveAsync(pending, BrokerOrderStatus.PlacePending, BrokerOrderStatus.OutcomeUnknown);
        await SaveAsync(filled, BrokerOrderStatus.Working, BrokerOrderStatus.Filled);
        await SaveAsync(unrelated, BrokerOrderStatus.Dispatched);

        var restartedStore = new BrokerOrderReadStore();
        Assert.Empty(restartedStore.List(trade));
        await new BrokerOrderReadModelRebuilder(fixture.Transactions).RebuildAsync(restartedStore);

        var context = Substitute.For<IBrokerOrderQueryContext>();
        context.Store.Returns(restartedStore);
        var projected = await new GetBrokerOrdersForTradeOrderQuery { TradeOrderId = trade }
            .ExecuteAsync(context, CancellationToken.None);
        Assert.True(projected.Success);
        Assert.NotNull(projected.Value);
        Assert.Equal(2, projected.Value.Length);
        Assert.Contains(projected.Value, order => order.Id == pending && order.Status == BrokerOrderStatus.OutcomeUnknown);
        Assert.Contains(projected.Value, order => order.Id == filled && order.Status == BrokerOrderStatus.Filled);
        Assert.True(restartedStore.TryGet(pending, out var pendingOrder));
        Assert.Equal(BrokerOrderStatus.OutcomeUnknown, pendingOrder.Status);
        Assert.True(restartedStore.TryGet(filled, out var filledOrder));
        Assert.Equal(BrokerOrderStatus.Filled, filledOrder.Status);
        Assert.DoesNotContain(projected.Value, order => order.Id == unrelated);
    }

    private async Task SaveAsync(BrokerOrderId id, params BrokerOrderStatus[] statuses)
    {
        await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            for (var index = 0; index < statuses.Length; index++)
            {
                var commandId = Guid.NewGuid();
                var snapshot = new BrokerOrderChangedEvent
                {
                    Id = Guid.NewGuid(), CommandId = commandId, EntityId = id,
                    AggregateId = id.Format(), ReceivedOn = DateTime.UtcNow,
                    BrokerOrderDefinition = new() { Id = id, Status = statuses[index], Revision = 1 }
                };
                await transaction.AppendAsync($"BrokerOrder.{id.Format()}", commandId, snapshot, index, token);
            }
            return true;
        });
    }
}
