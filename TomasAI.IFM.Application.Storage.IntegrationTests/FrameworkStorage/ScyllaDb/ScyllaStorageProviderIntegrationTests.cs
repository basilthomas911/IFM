using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Exceptions;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage.ScyllaDb;

[Collection(ScyllaStorageProviderCollection.Name)]
[Trait("Category", "ScyllaDBIntegration")]
public sealed class ScyllaStorageProviderIntegrationTests(ScyllaStorageProviderFixture fixture)
{
    const string InsertFund = """
        INSERT INTO fund (fundId, name, description, balance, isProduction, createdOn, createdBy)
        VALUES (:fundId, :name, :description, :balance, :isProduction, :createdOn, :createdBy);
        """;

    const string InsertFundOrder = """
        INSERT INTO fund_order (
            fundId, orderId, orderDate, orderStatus, baseContractId, tradeDate, maturityDate,
            reference, createdOn, createdBy, updatedOn, updatedBy)
        VALUES (
            :fundId, :orderId, :orderDate, :orderStatus, :baseContractId, :tradeDate, :maturityDate,
            :reference, :createdOn, :createdBy, :updatedOn, :updatedBy);
        """;

    const string InsertFundOrderTrade = """
        INSERT INTO fund_order_trade (
            fundId, orderId, tradeId, tradeType, tradeDate, maturityDate, tradeState, tradeAction,
            reference, primaryTrade, baseContractSymbol, createdOn, createdBy, updatedOn, updatedBy)
        VALUES (
            :fundId, :orderId, :tradeId, :tradeType, :tradeDate, :maturityDate, :tradeState, :tradeAction,
            :reference, :primaryTrade, :baseContractSymbol, :createdOn, :createdBy, :updatedOn, :updatedBy);
        """;

    const string InsertFundTransaction = """
        INSERT INTO fund_transaction (
            transactionId, transactionDate, transactionType, fundId, orderId, tradeId, tradeType,
            valueDate, tradeStatus, description, amount, balance)
        VALUES (
            :transactionId, :transactionDate, :transactionType, :fundId, :orderId, :tradeId, :tradeType,
            :valueDate, :tradeStatus, :description, :amount, :balance);
        """;

    const string SelectFund = """
        SELECT fundId AS ignored_6, name AS ignored_5, description AS ignored_4,
               balance AS ignored_3, isProduction AS ignored_2, createdOn AS ignored_1,
               createdBy AS ignored_0
        FROM fund WHERE fundId = :fundId;
        """;

    const string SelectFundOrders = """
        SELECT fundId, orderId, orderDate, orderStatus, baseContractId, tradeDate, maturityDate,
               reference, createdOn, createdBy, updatedOn, updatedBy
        FROM fund_order WHERE fundId = :fundId;
        """;

    const string SelectFundOrderTrade = """
        SELECT fundId, orderId, tradeId, tradeType, tradeDate, maturityDate, tradeState, tradeAction,
               reference, primaryTrade, baseContractSymbol, createdOn, createdBy, updatedOn, updatedBy
        FROM fund_order_trade WHERE fundId = :fundId AND orderId = :orderId AND tradeId = :tradeId;
        """;

    const string SelectFundTransaction = """
        SELECT transactionId, transactionDate, transactionType, fundId, orderId, tradeId, tradeType,
               valueDate, tradeStatus, description, amount, balance
        FROM fund_transaction
        WHERE fundId = :fundId AND valueDate = :valueDate AND orderId = :orderId
          AND tradeId = :tradeId AND tradeType = :tradeType AND transactionType = :transactionType
          AND transactionDate = :transactionDate AND transactionId = :transactionId;
        """;

    [Fact]
    public Task ExecuteCommandAsync_WithoutParameters_ExecutesSimpleStatement()
    {
        var scope = ScyllaFundTestData.Scope(1);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertFundAsync(repository, scope);

            var result = await repository
                .Use($"DELETE FROM fund WHERE fundId = {scope.FundId};")
                .ExecuteCommandAsync();

            Assert.Equal([-1L], result);
            Assert.Null(await GetFundAsync(repository, scope.FundId));
        });
    }

    [Fact]
    public Task ExecuteCommandAsync_WithBindValue_InsertsOneRow()
    {
        var scope = ScyllaFundTestData.Scope(2);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var result = await repository.Use(InsertFund)
                .SetParameters(new InsertFundBindValue(scope.FundId, 1200.50m))
                .ExecuteCommandAsync();

            Assert.Equal([-1L], result);
            var fund = await GetFundAsync(repository, scope.FundId);
            Assert.NotNull(fund);
            Assert.Equal(1200.50m, fund.Balance);
        });
    }

    [Fact]
    public Task ExecuteCommandAsync_WithManyParameterValues_ExecutesBoundedConcurrentWrites()
    {
        var scope = ScyllaFundTestData.Scope(3);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var parameters = new object?[][]
            {
                CreateOrderBindValues(scope, scope.OrderId, "Open"),
                CreateOrderBindValues(scope, scope.SecondOrderId, "Closed")
            };

            var result = await repository.Use(InsertFundOrder)
                .SetParameters(parameters)
                .ExecuteCommandAsync();

            Assert.Equal([-1L], result);
            var orders = await GetFundOrdersAsync(repository, scope.FundId);
            Assert.Equal(2, orders.Count);
        });
    }

    [Fact]
    public Task ExecuteCommandAsync_WithLargeDeferredParameterSequence_ExecutesEveryRow()
    {
        var scope = ScyllaFundTestData.Scope(3);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var enumerationCount = 0;
            var parameters = GetParameters();

            await repository.Use(InsertFundOrder)
                .SetParameters(parameters)
                .ExecuteCommandAsync();

            var orders = await GetFundOrdersAsync(repository, scope.FundId);
            Assert.Equal(256, orders.Count);
            Assert.Equal(256, enumerationCount);

            IEnumerable<object?[]> GetParameters()
            {
                for (var index = 0; index < 256; index++)
                {
                    enumerationCount++;
                    yield return CreateOrderBindValues(scope, scope.OrderId + index, "Open");
                }
            }
        });
    }

    [Fact]
    public Task ExecuteCommandAsync_WithCancelledToken_DoesNotEnumerateOrWriteRows()
    {
        var scope = ScyllaFundTestData.Scope(3);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var enumerationCount = 0;
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.Use(InsertFundOrder)
                .SetParameters(GetParameters())
                .ExecuteCommandAsync(cancellation.Token));

            Assert.Equal(0, enumerationCount);
            Assert.Empty(await GetFundOrdersAsync(repository, scope.FundId));

            IEnumerable<object?[]> GetParameters()
            {
                enumerationCount++;
                yield return CreateOrderBindValues(scope, scope.OrderId, "Open");
            }
        });
    }

    [Fact]
    public Task ExecuteQueuedCommandsAsync_Sequential_ExecutesEveryQueuedCommand()
    {
        var scope = ScyllaFundTestData.Scope(4);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var queuedCommands = new List<object>
            {
                repository.Use(InsertFund)
                    .SetParameters(CreateFundParameters(scope.FundId, 100m))
                    .QueueCommand(),
                repository.Use("UPDATE fund SET balance = :balance WHERE fundId = :fundId;")
                    .SetParameters(new UpdateFundBalanceParameters(225m, scope.FundId))
                    .QueueCommand()
            };

            await repository.ExecuteQueuedCommandsAsync(queuedCommands, useTransaction: false);

            var fund = await GetFundAsync(repository, scope.FundId);
            Assert.NotNull(fund);
            Assert.Equal(225m, fund.Balance);
        });
    }

    [Fact]
    public Task ExecuteQueuedCommandsAsync_LoggedBatch_ExecutesEveryQueuedCommand()
    {
        var scope = ScyllaFundTestData.Scope(5);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var queuedCommands = new List<object>
            {
                repository.Use(InsertFund)
                    .SetParameters(CreateFundParameters(scope.FundId, 300m))
                    .QueueCommand(),
                repository.Use("UPDATE fund SET balance = :balance WHERE fundId = :fundId;")
                    .SetParameters(new UpdateFundBalanceParameters(450m, scope.FundId))
                    .QueueCommand()
            };

            await repository.ExecuteQueuedCommandsAsync(queuedCommands, useTransaction: true);

            var fund = await GetFundAsync(repository, scope.FundId);
            Assert.NotNull(fund);
            Assert.Equal(450m, fund.Balance);
        });
    }

    [Fact]
    public Task ExecuteQueuedCommandsAsync_LoggedBatch_ExecutesEveryBindValue()
    {
        var scope = ScyllaFundTestData.Scope(5);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var queuedCommands = new List<object>
            {
                repository.Use(InsertFundOrder)
                    .SetParameters(new object?[][]
                    {
                        CreateOrderBindValues(scope, scope.OrderId, "Open"),
                        CreateOrderBindValues(scope, scope.SecondOrderId, "Closed")
                    })
                    .QueueCommand()
            };

            await repository.ExecuteQueuedCommandsAsync(queuedCommands, useTransaction: true);

            var orders = await GetFundOrdersAsync(repository, scope.FundId);
            Assert.Equal(2, orders.Count);
        });
    }

    [Fact]
    public Task ExecuteQueryAsync_UsesOrdinalMapperForMultipleRows()
    {
        var scope = ScyllaFundTestData.Scope(6);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertOrdersAsync(repository, scope);

            var rows = await repository.Use(SelectFundOrders)
                .SetParameters(new FundLookup(scope.FundId))
                .ExecuteQueryAsync(MapOrder);

            Assert.Equal(2, rows.Count);
            Assert.Contains(rows, row => row.OrderId == scope.OrderId && row.Status == TestOrderStatus.Open);
            Assert.Contains(rows, row => row.OrderId == scope.SecondOrderId && row.Status == TestOrderStatus.Closed);
        });
    }

    [Fact]
    public Task ExecuteQueryImmutableAsync_ReturnsDisposablePooledOrdinalResults()
    {
        var scope = ScyllaFundTestData.Scope(7);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertOrdersAsync(repository, scope);

            var rows = await repository.Use(SelectFundOrders)
                .SetParameters(new FundLookup(scope.FundId))
                .ExecuteQueryImmutableAsync(static row => new ImmutableOrderRow(
                    row.GetInt(0), row.GetInt(1), row.GetEnum<TestOrderStatus>(3)));

            using var pooledRows = Assert.IsType<PooledReadOnlyBuffer<ImmutableOrderRow>>(rows);
            Assert.Equal(2, pooledRows.Count);
            Assert.Equal(scope.FundId, pooledRows[0].FundId);
        });
    }

    [Fact]
    public Task ExecuteSingleAsync_ReturnsMappedRowOrNull()
    {
        var scope = ScyllaFundTestData.Scope(8);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertFundAsync(repository, scope);

            var existing = await GetFundAsync(repository, scope.FundId);
            var missing = await GetFundAsync(repository, scope.FundId - 1000);

            Assert.NotNull(existing);
            Assert.Equal(scope.FundId, existing.FundId);
            Assert.Null(missing);
        });
    }

    [Fact]
    public Task ExecuteScalarAsync_MapsFirstColumnByOrdinal()
    {
        var scope = ScyllaFundTestData.Scope(9);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertFundAsync(repository, scope, 9876.54m);

            var balance = await repository
                .Use("SELECT balance AS deliberately_not_value FROM fund WHERE fundId = :fundId;")
                .SetParameters(new FundLookup(scope.FundId))
                .ExecuteScalarAsync(static row => row.GetDecimal(0));

            Assert.Equal(9876.54m, balance);
        });
    }

    [Fact]
    public Task ExecuteMapReduceAsync_StreamsOrdinalResultsIntoReducer()
    {
        var scope = ScyllaFundTestData.Scope(10);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertOrdersAsync(repository, scope);
            var reducerCalls = 0;
            var orderIdSum = 0;

            await repository.Use("SELECT orderId FROM fund_order WHERE fundId = :fundId;")
                .SetParameters(new FundLookup(scope.FundId))
                .ExecuteMapReduceAsync(
                    static row => row.GetInt(0),
                    rows =>
                    {
                        reducerCalls++;
                        orderIdSum = rows.Sum();
                    });

            Assert.Equal(1, reducerCalls);
            Assert.Equal(scope.OrderId + scope.SecondOrderId, orderIdSum);
        });
    }

    [Fact]
    public Task ExecuteStreamAsync_StreamsEveryOrdinalResult()
    {
        var scope = ScyllaFundTestData.Scope(16);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertOrdersAsync(repository, scope);
            var orderIds = new List<int>();

            await foreach (var row in repository.Use(SelectFundOrders)
                .SetParameters(new FundLookup(scope.FundId))
                .ExecuteStreamAsync(MapOrder))
            {
                orderIds.Add(row.OrderId);
            }

            Assert.Equal(
                [scope.SecondOrderId, scope.OrderId],
                orderIds.OrderBy(orderId => orderId).ToArray());
        });
    }

    [Fact]
    public Task ExecuteStreamAsync_EarlyTerminationReleasesRowSet()
    {
        var scope = ScyllaFundTestData.Scope(16);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertOrdersAsync(repository, scope);
            var stream = repository.Use(SelectFundOrders)
                .SetParameters(new FundLookup(scope.FundId))
                .ExecuteStreamAsync(MapOrder);

            await using (var rows = stream.GetAsyncEnumerator())
            {
                Assert.True(await rows.MoveNextAsync());
                Assert.Equal(scope.FundId, rows.Current.FundId);
            }

            Assert.Equal(2, (await GetFundOrdersAsync(repository, scope.FundId)).Count);
        });
    }

    [Fact]
    public Task ExecuteStreamAsync_CancellationStopsEnumerationAndReleasesRowSet()
    {
        var scope = ScyllaFundTestData.Scope(16);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertOrdersAsync(repository, scope);
            using var cancellation = new CancellationTokenSource();
            var rowsRead = 0;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in repository.Use(SelectFundOrders)
                    .SetParameters(new FundLookup(scope.FundId))
                    .ExecuteStreamAsync(MapOrder, cancellation.Token))
                {
                    rowsRead++;
                    cancellation.Cancel();
                }
            });

            Assert.Equal(1, rowsRead);
            Assert.Equal(2, (await GetFundOrdersAsync(repository, scope.FundId)).Count);
        });
    }

    [Fact]
    public Task OrdinalMapping_ReadsEveryFundTableAndSupportedFundType()
    {
        var scope = ScyllaFundTestData.Scope(11);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertFundAsync(repository, scope, 5432.10m);
            await repository.Use(InsertFundOrder)
                .SetParameters(CreateOrderParameters(scope, scope.OrderId, "Open"))
                .ExecuteCommandAsync();
            await repository.Use(InsertFundOrderTrade)
                .SetParameters(CreateTradeParameters(scope))
                .ExecuteCommandAsync();
            await repository.Use(InsertFundTransaction)
                .SetParameters(CreateTransactionParameters(scope))
                .ExecuteCommandAsync();

            var fund = await GetFundAsync(repository, scope.FundId);
            var order = await repository.Use(SelectFundOrders)
                .SetParameters(new FundLookup(scope.FundId))
                .ExecuteSingleAsync(MapOrder);
            var trade = await repository.Use(SelectFundOrderTrade)
                .SetParameters(new FundOrderTradeLookup(scope.FundId, scope.OrderId, scope.TradeId))
                .ExecuteSingleAsync(MapTrade);
            var transaction = await repository.Use(SelectFundTransaction)
                .SetParameters(new FundTransactionLookup(
                    scope.FundId,
                    ScyllaFundTestData.ValueDate,
                    scope.OrderId,
                    scope.TradeId,
                    "LongIronCondor",
                    "OpeningTrade",
                    ScyllaFundTestData.CreatedOn,
                    scope.TransactionId))
                .ExecuteSingleAsync(MapTransaction);

            Assert.NotNull(fund);
            Assert.Equal((scope.FundId, "Scylla ordinal test", 5432.10m, false),
                (fund.FundId, fund.Name, fund.Balance, fund.IsProduction));
            Assert.Equal(ScyllaFundTestData.CreatedOn.Ticks, fund.CreatedOn.Ticks);

            Assert.NotNull(order);
            Assert.Equal(TestOrderStatus.Open, order.Status);
            Assert.Equal(ScyllaFundTestData.TradeDate, order.TradeDate);

            Assert.NotNull(trade);
            Assert.True(trade.PrimaryTrade);
            Assert.Equal("LongIronCondor", trade.TradeType);
            Assert.Equal(ScyllaFundTestData.MaturityDate, trade.MaturityDate);

            Assert.NotNull(transaction);
            Assert.Equal(scope.TransactionId, transaction.TransactionId);
            Assert.Equal(125.75m, transaction.Amount);
            Assert.Equal(5557.85m, transaction.Balance);
            Assert.Equal(ScyllaFundTestData.ValueDate, transaction.ValueDate);
        });
    }

    [Fact]
    public Task QueryMethods_RejectMoreThanOneParameterValue()
    {
        var scope = ScyllaFundTestData.Scope(12);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var context = repository.Use(SelectFund)
                .SetParameters(new[] { new FundLookup(scope.FundId), new FundLookup(scope.FundId - 1) });

            var exception = await Assert.ThrowsAsync<StorageException>(
                () => context.ExecuteQueryAsync(MapFund));

            Assert.Contains("only single parameter value accepted", exception.Message);
        });
    }

    [Fact]
    public Task ExecuteQueuedCommandsAsync_RejectsEmptyQueue()
    {
        var scope = ScyllaFundTestData.Scope(13);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var exception = await Assert.ThrowsAsync<StorageException>(
                () => repository.ExecuteQueuedCommandsAsync([]));

            Assert.Contains("no commands have been queued", exception.Message);
        });
    }

    [Fact]
    public Task MappingMethods_RejectNullDelegates()
    {
        var scope = ScyllaFundTestData.Scope(14);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            Assert.Throws<ArgumentNullException>(
                () => repository.Use(SelectFund).ExecuteStreamAsync<FundRow>(null!));
            await Assert.ThrowsAsync<StorageException>(
                () => repository.Use(SelectFund).ExecuteQueryAsync<FundRow>(null!));
            await Assert.ThrowsAsync<StorageException>(
                () => repository.Use(SelectFund).ExecuteSingleAsync<FundRow>(null!));
            await Assert.ThrowsAsync<StorageException>(
                () => repository.Use(SelectFund).ExecuteQueryImmutableAsync<ImmutableOrderRow>(null!));
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await repository.Use(SelectFund).ExecuteMapReduceAsync<int>(null!, _ => { }));
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await repository.Use(SelectFund).ExecuteMapReduceAsync(static row => row.GetInt(0), null!));
        });
    }

    static Task InsertFundAsync(ScyllaTestRepository repository, ScyllaFundTestScope scope, decimal balance = 1000m)
        => repository.Use(InsertFund)
            .SetParameters(CreateFundParameters(scope.FundId, balance))
            .ExecuteCommandAsync();

    static async Task InsertOrdersAsync(ScyllaTestRepository repository, ScyllaFundTestScope scope)
    {
        await repository.Use(InsertFundOrder)
            .SetParameters(new[]
            {
                CreateOrderParameters(scope, scope.OrderId, "Open"),
                CreateOrderParameters(scope, scope.SecondOrderId, "Closed")
            })
            .ExecuteCommandAsync();
    }

    static FundParameters CreateFundParameters(int fundId, decimal balance)
        => new(fundId, balance);

    static OrderParameters CreateOrderParameters(ScyllaFundTestScope scope, int orderId, string status)
        => new(
            scope.FundId,
            orderId,
            ScyllaFundTestData.CreatedOn,
            status,
            "ES-TEST",
            ScyllaFundTestData.TradeDate,
            ScyllaFundTestData.MaturityDate,
            "ordinal-order",
            ScyllaFundTestData.CreatedOn,
            "framework-storage-scylla-it",
            ScyllaFundTestData.UpdatedOn,
            "framework-storage-scylla-it");

    static object?[] CreateOrderBindValues(ScyllaFundTestScope scope, int orderId, string status)
        =>
        [
            scope.FundId,
            orderId,
            ScyllaFundTestData.CreatedOn,
            status,
            "ES-TEST",
            ScyllaFundTestData.TradeDate,
            ScyllaFundTestData.MaturityDate,
            "ordinal-order",
            ScyllaFundTestData.CreatedOn,
            "framework-storage-scylla-it",
            ScyllaFundTestData.UpdatedOn,
            "framework-storage-scylla-it"
        ];

    static TradeParameters CreateTradeParameters(ScyllaFundTestScope scope)
        => new(scope.FundId, scope.OrderId, scope.TradeId);

    static TransactionParameters CreateTransactionParameters(ScyllaFundTestScope scope)
        => new(scope.TransactionId, scope.FundId, scope.OrderId, scope.TradeId);

    static Task<FundRow?> GetFundAsync(ScyllaTestRepository repository, int fundId)
        => repository.Use(SelectFund)
            .SetParameters(new FundLookup(fundId))
            .ExecuteSingleAsync(MapFund);

    static Task<ICollection<OrderRow>> GetFundOrdersAsync(ScyllaTestRepository repository, int fundId)
        => repository.Use(SelectFundOrders)
            .SetParameters(new FundLookup(fundId))
            .ExecuteQueryAsync(MapOrder);

    static FundRow MapFund(IObjectDataRecord row) => new(
        row.GetInt(0),
        row.GetString(1),
        row.GetString(2),
        row.GetDecimal(3),
        row.GetBool(4),
        row.GetDateTime(5),
        row.GetString(6));

    static OrderRow MapOrder(IObjectDataRecord row) => new(
        row.GetInt(0),
        row.GetInt(1),
        row.GetDateTime(2),
        row.GetEnum<TestOrderStatus>(3),
        row.GetString(4),
        row.GetDateOnly(5),
        row.GetDateOnly(6),
        row.GetString(7),
        row.GetDateTime(8),
        row.GetString(9),
        row.GetDateTime(10),
        row.GetString(11));

    static TradeRow MapTrade(IObjectDataRecord row) => new(
        row.GetInt(0),
        row.GetInt(1),
        row.GetInt(2),
        row.GetString(3),
        row.GetDateOnly(4),
        row.GetDateOnly(5),
        row.GetString(6),
        row.GetString(7),
        row.GetString(8),
        row.GetBool(9),
        row.GetString(10),
        row.GetDateTime(11),
        row.GetString(12),
        row.GetDateTime(13),
        row.GetString(14));

    static TransactionRow MapTransaction(IObjectDataRecord row) => new(
        row.GetLong(0),
        row.GetDateTime(1),
        row.GetString(2),
        row.GetInt(3),
        row.GetInt(4),
        row.GetInt(5),
        row.GetString(6),
        row.GetDateOnly(7),
        row.GetString(8),
        row.GetString(9),
        row.GetDecimal(10),
        row.GetDecimal(11));

    readonly record struct InsertFundBindValue(int FundId, decimal Balance) : IBindValue
    {
        public object Bind() =>
        new object?[]
        {
            FundId,
            "Scylla integration fund",
            "Framework.Storage provider coverage",
            Balance,
            false,
            ScyllaFundTestData.CreatedOn,
            "framework-storage-scylla-it"
        };
    }

    readonly record struct FundLookup(int FundId) : IBindValue
    {
        public object Bind() => new object?[] { FundId };
    }

    readonly record struct UpdateFundBalanceParameters(decimal Balance, int FundId) : IBindValue
    {
        public object Bind() => new object?[] { Balance, FundId };
    }

    readonly record struct FundOrderTradeLookup(int FundId, int OrderId, int TradeId) : IBindValue
    {
        public object Bind() => new object?[] { FundId, OrderId, TradeId };
    }

    readonly record struct FundTransactionLookup(
        int FundId,
        DateOnly ValueDate,
        int OrderId,
        int TradeId,
        string TradeType,
        string TransactionType,
        DateTime TransactionDate,
        long TransactionId) : IBindValue
    {
        public object Bind() => new object?[]
        {
            FundId, ValueDate, OrderId, TradeId, TradeType, TransactionType, TransactionDate, TransactionId
        };
    }

    readonly record struct FundParameters(int FundId, decimal Balance) : IBindValue
    {
        public object Bind() => new object?[]
        {
            FundId,
            "Scylla ordinal test",
            "Framework.Storage integration test data",
            Balance,
            false,
            ScyllaFundTestData.CreatedOn,
            "framework-storage-scylla-it"
        };
    }

    readonly record struct OrderParameters(
        int fundId,
        int orderId,
        DateTime orderDate,
        string orderStatus,
        string baseContractId,
        DateOnly tradeDate,
        DateOnly maturityDate,
        string reference,
        DateTime createdOn,
        string createdBy,
        DateTime updatedOn,
        string updatedBy) : IBindValue
    {
        public object Bind() => new object?[]
        {
            fundId, orderId, orderDate, orderStatus, baseContractId, tradeDate, maturityDate,
            reference, createdOn, createdBy, updatedOn, updatedBy
        };
    }

    readonly record struct TradeParameters(int FundId, int OrderId, int TradeId) : IBindValue
    {
        public object Bind() => new object?[]
        {
            FundId, OrderId, TradeId, "LongIronCondor", ScyllaFundTestData.TradeDate,
            ScyllaFundTestData.MaturityDate, "NewTrade", "Buy", "ordinal-trade", true, "ES",
            ScyllaFundTestData.CreatedOn, "framework-storage-scylla-it", ScyllaFundTestData.UpdatedOn,
            "framework-storage-scylla-it"
        };
    }

    readonly record struct TransactionParameters(long TransactionId, int FundId, int OrderId, int TradeId) : IBindValue
    {
        public object Bind() => new object?[]
        {
            TransactionId, ScyllaFundTestData.CreatedOn, "OpeningTrade", FundId, OrderId, TradeId,
            "LongIronCondor", ScyllaFundTestData.ValueDate, "Open", "ordinal-transaction", 125.75m, 5557.85m
        };
    }

    sealed record FundRow(
        int FundId,
        string Name,
        string Description,
        decimal Balance,
        bool IsProduction,
        DateTime CreatedOn,
        string CreatedBy);

    sealed record OrderRow(
        int FundId,
        int OrderId,
        DateTime OrderDate,
        TestOrderStatus Status,
        string BaseContractId,
        DateOnly TradeDate,
        DateOnly MaturityDate,
        string Reference,
        DateTime CreatedOn,
        string CreatedBy,
        DateTime UpdatedOn,
        string UpdatedBy);

    readonly record struct ImmutableOrderRow(int FundId, int OrderId, TestOrderStatus Status);

    sealed record TradeRow(
        int FundId,
        int OrderId,
        int TradeId,
        string TradeType,
        DateOnly TradeDate,
        DateOnly MaturityDate,
        string TradeState,
        string TradeAction,
        string Reference,
        bool PrimaryTrade,
        string BaseContractSymbol,
        DateTime CreatedOn,
        string CreatedBy,
        DateTime UpdatedOn,
        string UpdatedBy);

    sealed record TransactionRow(
        long TransactionId,
        DateTime TransactionDate,
        string TransactionType,
        int FundId,
        int OrderId,
        int TradeId,
        string TradeType,
        DateOnly ValueDate,
        string TradeStatus,
        string Description,
        decimal Amount,
        decimal Balance);

    enum TestOrderStatus
    {
        Open,
        Closed
    }
}
