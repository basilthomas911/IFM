using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Fund.Command.State;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FundDb;

public class FundDatabaseFixture : IDisposable
{
    public Storage.FundDb.FundDbContext FundDb { get; private set; }
    public SequenceIdDbContext SeqIdDatabase { get; private set; }
    public ISequenceIdGenerator SequenceIdGenerator { get; private set; }

    public IDbContextFactory DbFactory { get; private set; }

    public FundDatabaseFixture()
    {
        SetSeqIdDatabase();
        SetFundDatabase();
    }

    void SetFundDatabase()
    {
        var dbConn = new DbConnectionSettings()
                         .Add("FundDbConnection", "Contact Points=localhost;Port=9042;Default Keyspace=fund_test_db", "System.Data.ScyllaDb");

        var diContainer = new Dictionary<Type, Storage.FundDb.FundDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        new TomasAI.IFM.Application.Storage.FundDb.Schema.FundSchemaDb(dbConn, logger)
            .CreateAllAsync().GetAwaiter().GetResult();
        var redisCache = Substitute.For<IRedisCache>();
        var redisCacheMap = new Dictionary<string, string>();
        redisCache.Get(Arg.Any<string>()).Returns(callInfo => redisCacheMap[callInfo.Arg<string>()]);
        redisCache.When(_ => _.Set(Arg.Any<string>(), Arg.Any<string>())).Do(_ => { redisCacheMap.Add(_.ArgAt<string>(0), _.ArgAt<string>(1)); });
        var blackboardServce = new BlackboardService(redisCache, new SystemTextJsonSerializer());
        DbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<Storage.FundDb.FundDbContext>), new Storage.FundDb.FundDbContext(dbConn, DbFactory, SequenceIdGenerator, logger));
        FundDb = DbFactory.FundDb as Storage.FundDb.FundDbContext;

    }

    void SetSeqIdDatabase()
    {
        var dbConn = new DbConnectionSettings()
             .Add("SequenceIdDbConnection", "Host=localhost;Port=5432;Database=sequence-id-test-db", "System.Data.Postgres");
        var diContainer = new Dictionary<Type, SequenceIdDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        var dbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        diContainer.Add(typeof(IObjectRepository<SequenceIdDbContext>), new SequenceIdDbContext(dbConn, dbFactory, logger));
        SeqIdDatabase = dbFactory.SequenceIdDb as SequenceIdDbContext;
        SequenceIdDatabaseInitializer.EnsureInitialized(new TomasAI.IFM.Application.Storage.SequenceIdDb.Schema.SequenceIdSchemaDb(dbConn, logger));
        SequenceIdGenerator = new PostgresSequenceIdGenerator(dbFactory.SequenceIdDb as SequenceIdDbContext);
    }

    public void Dispose()
    {
    }
}

public class FundDbTests(FundDatabaseFixture testFixture) 
    : IClassFixture<FundDatabaseFixture>
{
     readonly FundDatabaseFixture _testFixture = testFixture;

    async Task ResetFundTransactionsAsync()
    {
        await _testFixture.FundDb.UseTest("TRUNCATE TABLE fund_transaction;").ExecuteCommandAsync();
        await _testFixture.FundDb.UseTest("TRUNCATE TABLE fund_transaction_identity_v4;").ExecuteCommandAsync();
        await ResetFundTransactionProjectionsAsync();
    }

    async Task ResetFundTransactionProjectionsAsync()
    {
        await _testFixture.FundDb.UseTest("TRUNCATE TABLE fund_transaction_timeline_v3;").ExecuteCommandAsync();
        await _testFixture.FundDb.UseTest("TRUNCATE TABLE fund_balance_by_status_day_v3;").ExecuteCommandAsync();
        await _testFixture.FundDb.UseTest("TRUNCATE TABLE fund_transaction_amount_v3;").ExecuteCommandAsync();
        await _testFixture.FundDb.UseTest("TRUNCATE TABLE fund_transaction_projection_state_v3;").ExecuteCommandAsync();
        await _testFixture.FundDb.UseTest("TRUNCATE TABLE fund_transaction_projection_mutation_v3;").ExecuteCommandAsync();
        await _testFixture.FundDb.UseTest("TRUNCATE TABLE fund_transaction_write_mutation_v3;").ExecuteCommandAsync();
        await _testFixture.FundDb.UseTest("TRUNCATE TABLE fund_transaction_write_ownership_v3;").ExecuteCommandAsync();
    }

    /*
    [Fact]
    [Trait("get a fund", "FundDb")]
    public async Task GetFundAsyncOk()
    {
        var dbFactory = _testFixture.DbFactory;
        await dbFactory.DeleteFundAsync(SampleData.Fund.FundId);
        await dbFactory.InsertFundAsync(SampleData.Fund);
        var fund = await dbFactory.GetFundAsync(SampleData.Fund.FundId);
        fund.Should().NotBeNull();
        fund.FundId.Should().Be(SampleData.Fund.FundId);
        fund.Name.Should().Be(SampleData.Fund.Name);
        fund.Description.Should().Be(SampleData.Fund.Description);
        fund.Balance.Should().Be(SampleData.Fund.Balance);
        fund.IsProduction.Should().Be(SampleData.Fund.IsProduction);
        DateOnly.FromDateTime(fund.CreatedOn).Should().Be(DateOnly.FromDateTime(SampleData.Fund.CreatedOn));
        fund.CreatedBy.Should().Be(SampleData.Fund.CreatedBy);
    }
    */

    [Fact]
    [Trait("get all funds", "FundDb")]
    public async Task GetFundsAsyncOk()
    {
        var db = _testFixture.FundDb;
        await db.UseTest($"delete from fund where FundId = {SampleData.Fund.FundId} ").ExecuteCommandAsync();
        await db.InsertFundAsync(SampleData.Fund);
        var funds = await db.GetFundsAsync();
        funds.Should().NotBeNull();
        funds .Count.Should().BeGreaterThanOrEqualTo(1);
        var fund = funds.Where(e => e.FundId == SampleData.Fund.FundId).SingleOrDefault();
        fund.Should().NotBeNull();
        fund.FundId.Should().Be(SampleData.Fund.FundId);
        fund.Name.Should().Be(SampleData.Fund.Name);
        fund.Description.Should().Be(SampleData.Fund.Description);
        fund.Balance.Should().Be(SampleData.Fund.Balance);
        fund.IsProduction.Should().Be(SampleData.Fund.IsProduction);
        DateOnly.FromDateTime(fund.CreatedOn).Should().Be(DateOnly.FromDateTime(SampleData.Fund.CreatedOn));
        fund.CreatedBy.Should().Be(SampleData.Fund.CreatedBy);
    }


    [Fact]
    [Trait("insert a fund", "FundDb")]
    public async Task InsertFundAsyncOk()
    {
        var db = _testFixture.FundDb;
        await db.UseTest($"delete from fund where FundId = {SampleData.Fund.FundId} ").ExecuteCommandAsync();
        await db.InsertFundAsync(SampleData.Fund);
        var fund = await db.GetFundAsync(SampleData.Fund.FundId);
        fund.Should().NotBeNull();
        fund.FundId.Should().Be(SampleData.Fund.FundId);
    }

    [Fact]
    [Trait("delete a fund", "FundDb")]
    public async Task DeleteFundAsyncOk()
    {
        var db = _testFixture.FundDb;
        await db.UseTest($"delete from fund where FundId = {SampleData.Fund.FundId} ").ExecuteCommandAsync();
        await db.InsertFundAsync(SampleData.Fund);
        await db.DeleteFundAsync(SampleData.Fund.FundId);
        var fund = await db.GetFundAsync(SampleData.Fund.FundId);
        fund.Should().BeNull();
    }

    [Fact]
    [Trait("insert a fund order", "FundDb")]
    public async Task InsertFundOrderAsyncOk()
    {
        var db = _testFixture.FundDb;
        await db.UseTest($"delete from fund_order where FundId = {SampleData.FundOrder.FundId}").ExecuteCommandAsync();
        await db.InsertFundOrderAsync(SampleData.FundOrder);
        var fundOrders = await db.GetFundOrdersAsync();
        fundOrders.Count.Should().BeGreaterThanOrEqualTo(1);    
        fundOrders.Where(e => e.FundId == SampleData.FundOrder.FundId && e.OrderId == SampleData.FundOrder.OrderId ).SingleOrDefault().Should().NotBeNull();
    }

    [Fact]
    [Trait("reject ambiguous fund order batch", "FundDb")]
    public async Task InsertFundOrdersAsync_RejectsConflictingDuplicatePayloads()
    {
        var orderId = Random.Shared.Next(1_500_000_000, 2_000_000_000);
        var first = SampleData.FundOrder with { OrderId = orderId, Reference = "first" };
        var conflicting = first with { Reference = "second" };

        await FluentActions.Awaiting(() => _testFixture.FundDb.InsertFundOrdersAsync(
                new[] { first, conflicting }))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    [Trait("delete a fund order", "FundDb")]
    public async Task DeleteFundOrderAsyncOk()
    {
        var db = _testFixture.FundDb;
        await db.UseTest($"delete from fund_order where FundId = {SampleData.Fund.FundId} ").ExecuteCommandAsync();
        await db.InsertFundOrderAsync(SampleData.FundOrder);
        var fundOrderId = FundOrderId.Create(SampleData.FundOrder.FundId, SampleData.FundOrder.OrderId);
         await db.DeleteFundOrderAsync(fundOrderId.FundId, fundOrderId.OrderId);
        var fundOrders = await db.GetFundOrdersAsync();
        fundOrders.Where(e => e.FundId == SampleData.FundOrder.FundId && e.OrderId == SampleData.FundOrder.OrderId).SingleOrDefault().Should().BeNull();
        fundOrders.Count.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    [Trait("online fund order projection backfill", "FundDb")]
    public async Task FundOrderBackfill_PreservesHistoricalProjectionOnlyReservationWithoutMismatch()
    {
        var db = _testFixture.FundDb;
        var orderId = Random.Shared.Next(1_500_000_000, 2_000_000_000);
        var fundId = Random.Shared.Next(1_000_000_000, 1_400_000_000);
        var reservationToken = Guid.NewGuid();
        var baseline = await db.BackfillFundOrderByOrderIdProjectionAsync();

        await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.InsertFundOrderByOrderIdV3)}", FundDbCql.InsertFundOrderByOrderIdV3)
            .SetParameters(new InsertFundOrderByOrderIdV3(orderId, fundId, reservationToken))
            .ExecuteCommandAsync();
        try
        {
            // Projection-only rows are valid historical ownership, including the
            // observable midpoint before a live insert publishes its canonical row.
            var result = await db.BackfillFundOrderByOrderIdProjectionAsync();

            result.ConflictingRows.Should().Be(baseline.ConflictingRows);
            result.MissingRows.Should().Be(baseline.MissingRows);
            result.TokenlessRows.Should().Be(baseline.TokenlessRows);
            result.IsReconciled.Should().Be(baseline.IsReconciled);
            var reservedFundId = await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.GetFundIdFromOrderId)}", FundDbCql.GetFundIdFromOrderId)
                .SetParameters(new GetFundIdFromOrderId(orderId))
                .ExecuteScalarAsync(static row => row.GetInt(0));
            reservedFundId.Should().Be(fundId,
                "an online backfill must not release an in-flight writer's uniqueness reservation");
        }
        finally
        {
            await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.DeleteFundOrderByOrderIdV3ForOfflineRepair)}", FundDbCql.DeleteFundOrderByOrderIdV3ForOfflineRepair)
                .SetParameters(new DeleteFundOrderByOrderIdV3ForOfflineRepair(orderId))
                .ExecuteCommandAsync();
        }
    }

    [Fact]
    [Trait("permanent fund order ownership", "FundDb")]
    public async Task DeleteFundOrder_RetainsReservationAndRejectsCrossFundReuse()
    {
        var db = _testFixture.FundDb;
        var orderId = Random.Shared.Next(1_500_000_000, 2_000_000_000);
        var fundId = Random.Shared.Next(1_000_000_000, 1_400_000_000);
        var order = SampleData.FundOrder with { FundId = fundId, OrderId = orderId };
        try
        {
            await db.InsertFundOrderAsync(order);
            await db.DeleteFundOrderAsync(fundId, orderId);

            (await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.GetFundIdFromOrderId)}", FundDbCql.GetFundIdFromOrderId)
                    .SetParameters(new GetFundIdFromOrderId(orderId))
                    .ExecuteScalarAsync(static row => row.GetInt(0)))
                .Should().Be(fundId);

            var replacement = order with { FundId = fundId + 1 };
            await FluentActions.Awaiting(() => db.InsertFundOrderAsync(replacement))
                .Should().ThrowAsync<global::TomasAI.IFM.Shared.Exceptions.StorageException>();
        }
        finally
        {
            await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.DeleteFundOrder)}", FundDbCql.DeleteFundOrder)
                .SetParameters(new DeleteFundOrder(fundId, orderId))
                .ExecuteCommandAsync();
            await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.DeleteFundOrderByOrderIdV3ForOfflineRepair)}", FundDbCql.DeleteFundOrderByOrderIdV3ForOfflineRepair)
                .SetParameters(new DeleteFundOrderByOrderIdV3ForOfflineRepair(orderId))
                .ExecuteCommandAsync();
        }
    }

    [Fact]
    [Trait("fund order distributed ownership", "FundDb")]
    public async Task FundOrderWrite_HeldOwnershipRejectsConcurrentDeleteThenAllowsRetry()
    {
        var db = _testFixture.FundDb;
        var orderId = Random.Shared.Next(1_500_000_000, 2_000_000_000);
        var fundId = Random.Shared.Next(1_000_000_000, 1_400_000_000);
        var order = SampleData.FundOrder with { FundId = fundId, OrderId = orderId };
        var ownershipHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeInsert = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        db.FundOrderCanonicalMutationSubmittingForTestingAsync = async () =>
        {
            ownershipHeld.TrySetResult();
            await resumeInsert.Task;
        };

        try
        {
            var insert = db.InsertFundOrderAsync(order);
            await ownershipHeld.Task.WaitAsync(TimeSpan.FromSeconds(15));

            await FluentActions.Awaiting(() => db.DeleteFundOrderAsync(fundId, orderId))
                .Should().ThrowAsync<global::TomasAI.IFM.Shared.Exceptions.StorageException>();

            resumeInsert.TrySetResult();
            await insert;

            await db.DeleteFundOrderAsync(fundId, orderId);
            (await db.GetFundOrderAsync(fundId, orderId)).Should().BeNull();
            (await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.GetFundIdFromOrderId)}", FundDbCql.GetFundIdFromOrderId)
                    .SetParameters(new GetFundIdFromOrderId(orderId))
                    .ExecuteScalarAsync(static row => row.GetInt(0)))
                .Should().Be(fundId, "successful recovery retains permanent historical ownership");
        }
        finally
        {
            resumeInsert.TrySetResult();
            db.FundOrderCanonicalMutationSubmittingForTestingAsync = null;
            await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.DeleteFundOrder)}", FundDbCql.DeleteFundOrder)
                .SetParameters(new DeleteFundOrder(fundId, orderId))
                .ExecuteCommandAsync();
            await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.DeleteFundOrderByOrderIdV3ForOfflineRepair)}", FundDbCql.DeleteFundOrderByOrderIdV3ForOfflineRepair)
                .SetParameters(new DeleteFundOrderByOrderIdV3ForOfflineRepair(orderId))
                .ExecuteCommandAsync();
        }
    }

    [Fact]
    [Trait("insert a fund order trade", "FundDb")]
    public async Task InsertFundOrderTradeAsyncOk()
    {
        var db = _testFixture.FundDb;
        await db.UseTest($"delete from fund_order_trade where FundId = {SampleData.FundOrderTrade.FundId} and OrderId = {SampleData.FundOrderTrade.OrderId}").ExecuteCommandAsync();
        await db.InsertFundOrderTradeAsync(SampleData.FundOrderTrade);
        var fundOrderTrades = await db.GetFundOrderTradesAsync();
        fundOrderTrades.Count.Should().BeGreaterThanOrEqualTo(1);
        fundOrderTrades.Where(e => e.FundId == SampleData.FundOrderTrade.FundId && e.OrderId == SampleData.FundOrderTrade.OrderId && e.TradeId == SampleData.FundOrderTrade.TradeId).SingleOrDefault().Should().NotBeNull();
    }

    [Fact]
    [Trait("delete a fund order trade", "FundDb")]
    public async Task DeleteFundOrderTradeAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundOrderTrade.FundId;
        var orderId = SampleData.FundOrderTrade.OrderId;
        var tradeId = 992934;
        await db.UseTest($"delete from fund_order_trade where FundId = {fundId} and OrderId = {orderId} and TradeId = {tradeId}").ExecuteCommandAsync();
        await db.InsertFundOrderTradeAsync(SampleData.FundOrderTrade with { TradeId = tradeId});
        await db.DeleteFundOrderTradeAsync(SampleData.FundOrderTrade.FundId, SampleData.FundOrderTrade.OrderId, tradeId);
        var fundOrderTrades = await db.GetFundOrderTradesAsync();
        fundOrderTrades.Where(e => e.FundId == fundId && e.OrderId == orderId && e.TradeId == tradeId).SingleOrDefault().Should().BeNull();
    }

    [Fact]
    [Trait("insert a fund transaction", "FundDb")]
    public async Task InsertFundTransactionAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundTransaction.FundId;
        var orderId = SampleData.FundTransaction.OrderId;
        var tradeId = SampleData.FundTransaction.TradeId;   
        var valueDate = SampleData.FundTransaction.ValueDate;
        await ResetFundTransactionsAsync();
        await db.InsertFundTransactionAsync(SampleData.FundTransaction);
        var fundTransactions = await db.GetFundTransactionsAsync(
            SampleData.FundTransaction.FundId,
            DateOnly.FromDateTime(SampleData.FundTransaction.TransactionDate.AddDays(-1)),
            DateOnly.FromDateTime(SampleData.FundTransaction.TransactionDate.AddDays(1)));
        fundTransactions.Count.Should().BeGreaterThanOrEqualTo(1);
        fundTransactions.Where(e => e.FundId == SampleData.FundTransaction.FundId && e.OrderId == SampleData.FundTransaction.OrderId && e.TradeId == SampleData.FundTransaction.TradeId).SingleOrDefault().Should().NotBeNull();
    }

    [Fact]
    [Trait("backfill fund transaction projections", "FundDb")]
    public async Task FundTransactionProjectionFallbackAndBackfillAsyncOk()
    {
        var db = _testFixture.FundDb;
        var transaction = SampleData.FundTransaction with
        {
            TransactionType = FundTransactionType.RealizedTradePnl,
            Amount = 123.45m
        };
        await ResetFundTransactionsAsync();
        await db.InsertFundTransactionAsync(transaction);
        await ResetFundTransactionProjectionsAsync();

        var fallbackPnl = await db.GetFundPnlAsync(transaction.FundId, transaction.ValueDate, transaction.ValueDate);
        fallbackPnl.Should().ContainSingle().Which.Pnl.Should().Be(transaction.Amount);

        var firstBackfill = await db.BackfillFundTransactionProjectionsAsync(
            transaction.FundId,
            transaction.ValueDate,
            transaction.ValueDate,
            batchSize: 1);
        firstBackfill.TransactionsRead.Should().Be(1);
        firstBackfill.TransactionsProjected.Should().Be(1);
        firstBackfill.BatchesExecuted.Should().Be(1);
        firstBackfill.TimelineRows.Should().Be(1);
        firstBackfill.StatusBalanceRows.Should().Be(1);
        firstBackfill.TransactionAmountRows.Should().Be(1);
        firstBackfill.IsReconciled.Should().BeTrue();

        var monthBucket = new DateOnly(transaction.ValueDate.Year, transaction.ValueDate.Month, 1);
        var activeMutationId = Guid.NewGuid();
        await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.InsertFundTransactionProjectionMutationV3)}", FundDbCql.InsertFundTransactionProjectionMutationV3)
            .SetParameters(new InsertFundTransactionProjectionMutationV3(
                transaction.FundId,
                monthBucket,
                activeMutationId,
                DateTime.UtcNow))
            .ExecuteCommandAsync();
        await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.InsertFundTransactionAmountV3)}", FundDbCql.InsertFundTransactionAmountV3)
            .SetParameters(new InsertFundTransactionAmountV3(
                transaction.FundId,
                monthBucket,
                transaction.TransactionType.ToString(),
                FundTransactionProjection.GetAmountSign(transaction.Amount),
                transaction.ValueDate,
                transaction.TransactionDate,
                1,
                transaction.OrderId,
                transaction.TradeId,
                transaction.TradeType.ToString(),
                999m))
            .ExecuteCommandAsync();

        var mutationFallbackPnl = await db.GetFundPnlAsync(transaction.FundId, transaction.ValueDate, transaction.ValueDate);
        mutationFallbackPnl.Should().ContainSingle().Which.Pnl.Should().Be(transaction.Amount);
        await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.DeleteFundTransactionProjectionMutationV3)}", FundDbCql.DeleteFundTransactionProjectionMutationV3)
            .SetParameters(new DeleteFundTransactionProjectionMutationV3(
                transaction.FundId,
                monthBucket,
                activeMutationId))
            .ExecuteCommandAsync();

        var staleTransactionId = long.MaxValue - 101;
        await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.InsertFundTransactionTimelineV3)}", FundDbCql.InsertFundTransactionTimelineV3)
            .SetParameters(new InsertFundTransactionTimelineV3(
                transaction.FundId,
                monthBucket,
                transaction.ValueDate,
                transaction.TransactionDate.AddTicks(1),
                staleTransactionId,
                transaction.TransactionType.ToString(),
                transaction.OrderId + 1000,
                transaction.TradeId,
                transaction.TradeType.ToString(),
                transaction.TradeStatus.ToString(),
                "stale",
                transaction.Amount,
                transaction.Balance))
            .ExecuteCommandAsync();
        await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.InsertFundBalanceByStatusDayV3)}", FundDbCql.InsertFundBalanceByStatusDayV3)
            .SetParameters(new InsertFundBalanceByStatusDayV3(
                transaction.FundId,
                monthBucket,
                transaction.ValueDate,
                transaction.TradeStatus.ToString(),
                transaction.TransactionDate.AddTicks(1),
                staleTransactionId,
                transaction.TransactionType.ToString(),
                transaction.OrderId + 1000,
                transaction.TradeId,
                transaction.TradeType.ToString(),
                transaction.Balance))
            .ExecuteCommandAsync();
        await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.InsertFundTransactionAmountV3)}", FundDbCql.InsertFundTransactionAmountV3)
            .SetParameters(new InsertFundTransactionAmountV3(
                transaction.FundId,
                monthBucket,
                transaction.TransactionType.ToString(),
                FundTransactionProjection.GetAmountSign(transaction.Amount),
                transaction.ValueDate,
                transaction.TransactionDate.AddTicks(1),
                staleTransactionId,
                transaction.OrderId + 1000,
                transaction.TradeId,
                transaction.TradeType.ToString(),
                transaction.Amount))
            .ExecuteCommandAsync();

        var replay = await db.BackfillFundTransactionProjectionsAsync(
            transaction.FundId,
            transaction.ValueDate,
            transaction.ValueDate,
            batchSize: 1);
        replay.Should().Be(firstBackfill);

        var projectedPnl = await db.GetFundPnlAsync(transaction.FundId, transaction.ValueDate, transaction.ValueDate);
        projectedPnl.Should().ContainSingle().Which.Pnl.Should().Be(transaction.Amount);
    }

    [Fact]
    [Trait("recover a stale fund projection mutation", "FundDb")]
    public async Task FundTransactionBackfill_ExplicitUtcCutoffRecoversOnlyJournaledStaleMutation()
    {
        var db = _testFixture.FundDb;
        var transaction = SampleData.FundTransaction with
        {
            TransactionId = 0,
            TransactionDate = new DateTime(2046, 4, 12, 10, 0, 0, DateTimeKind.Utc),
            ValueDate = new DateOnly(2046, 4, 12)
        };
        var monthBucket = new DateOnly(2046, 4, 1);
        var mutationId = Guid.NewGuid();
        var cutoffUtc = DateTime.UtcNow.AddMinutes(-5);
        var startedOn = cutoffUtc.AddMinutes(-1);
        await ResetFundTransactionsAsync();
        await db.InsertFundTransactionAsync(transaction);
        await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.InsertFundTransactionWriteMutationV3)}", FundDbCql.InsertFundTransactionWriteMutationV3)
            .SetParameters(new InsertFundTransactionWriteMutationV3(
                transaction.FundId,
                mutationId,
                startedOn))
            .ExecuteCommandAsync();
        await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.InsertFundTransactionProjectionMutationV3)}", FundDbCql.InsertFundTransactionProjectionMutationV3)
            .SetParameters(new InsertFundTransactionProjectionMutationV3(
                transaction.FundId,
                monthBucket,
                mutationId,
                startedOn))
            .ExecuteCommandAsync();
        _ = await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.ClaimFundTransactionWriteOwnershipV3)}", FundDbCql.ClaimFundTransactionWriteOwnershipV3)
            .SetParameters(new ClaimFundTransactionWriteOwnershipV3(
                transaction.FundId,
                mutationId,
                startedOn))
            .ExecuteScalarAsync(static row => row.GetBool(0));

        var result = await db.BackfillFundTransactionProjectionsAsync(
            transaction.FundId,
            transaction.ValueDate,
            transaction.ValueDate,
            batchSize: 1,
            cancellationToken: CancellationToken.None,
            staleOperationCutoffUtc: cutoffUtc);

        result.IsReconciled.Should().BeTrue();
        (await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.GetFundTransactionWriteMutationsV3)}", FundDbCql.GetFundTransactionWriteMutationsV3)
                .SetParameters(new GetFundTransactionWriteMutationsV3(transaction.FundId))
                .ExecuteQueryAsync(static row => row.GetGuid(0)))
            .Should().BeEmpty();
        (await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.GetFundTransactionProjectionMutationsV3)}", FundDbCql.GetFundTransactionProjectionMutationsV3)
                .SetParameters(new GetFundTransactionProjectionMutationsV3(
                    transaction.FundId,
                    monthBucket))
                .ExecuteQueryAsync(static row => row.GetGuid(0)))
            .Should().BeEmpty();
    }

    [Fact]
    [Trait("retry a fund transaction", "FundDb")]
    public async Task FundTransactionRetryReusesCanonicalTransactionIdAsyncOk()
    {
        var db = _testFixture.FundDb;
        var transaction = SampleData.FundTransaction with
        {
            TransactionId = 0,
            TransactionDate = new DateTime(2026, 7, 18, 12, 13, 14, DateTimeKind.Utc),
            ValueDate = new DateOnly(2026, 7, 18),
            Description = "stable retry"
        };
        await ResetFundTransactionsAsync();

        await db.InsertFundTransactionAsync(transaction);
        await db.InsertFundTransactionAsync(transaction);

        var canonicalRows = await db.GetFundTransactionsAsync();
        canonicalRows.Where(row =>
                row.FundId == transaction.FundId &&
                row.ValueDate == transaction.ValueDate &&
                row.OrderId == transaction.OrderId &&
                row.TradeId == transaction.TradeId &&
                row.TradeType == transaction.TradeType &&
                row.TransactionType == transaction.TransactionType &&
                row.TransactionDate == transaction.TransactionDate)
            .Should().ContainSingle();
    }

    [Fact]
    [Trait("concurrent retry uses one distributed fund transaction identity", "FundDb")]
    public async Task ConcurrentFundTransactionRetriesReserveOneCanonicalTransactionIdAsyncOk()
    {
        const int writerCount = 8;
        var db = _testFixture.FundDb;
        var transaction = SampleData.FundTransaction with
        {
            TransactionId = 0,
            TransactionDate = new DateTime(2048, 5, 19, 12, 13, 14, DateTimeKind.Utc),
            ValueDate = new DateOnly(2048, 5, 19),
            Description = "distributed identity retry"
        };
        await ResetFundTransactionsAsync();

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writers = Enumerable.Range(0, writerCount)
            .Select(async index =>
            {
                await start.Task.ConfigureAwait(false);
                await db.InsertFundTransactionAsync(transaction with
                {
                    TransactionId = 50_000 + index,
                    Description = $"distributed identity retry {index}",
                    Amount = transaction.Amount + index,
                    Balance = transaction.Balance + index
                });
            })
            .ToArray();
        start.SetResult();
        await Task.WhenAll(writers);

        var canonicalRows = (await db.GetFundTransactionsAsync())
            .Where(row => FundTransactionLogicalKey.From(row) == FundTransactionLogicalKey.From(transaction))
            .ToArray();
        canonicalRows.Should().ContainSingle();
        canonicalRows[0].TransactionId.Should().BeInRange(50_000, 50_000 + writerCount - 1);

        var reservedTransactionId = await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.GetFundTransactionIdentityV4)}", FundDbCql.GetFundTransactionIdentityV4)
            .SetParameters(new GetFundTransactionIdentityV4(
                transaction.FundId,
                transaction.ValueDate,
                transaction.OrderId,
                transaction.TradeId,
                transaction.TradeType.ToString(),
                transaction.TransactionType.ToString(),
                transaction.TransactionDate))
            .ExecuteScalarAsync(static row => row.GetLong(0));
        reservedTransactionId.Should().Be(canonicalRows[0].TransactionId);

        var monthBucket = FundTransactionProjection.GetMonthBucket(transaction.ValueDate);
        (await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.GetFundTransactionProjectionStateV3)}", FundDbCql.GetFundTransactionProjectionStateV3)
                .SetParameters(new GetFundTransactionProjectionStateV3(transaction.FundId, monthBucket))
                .ExecuteQueryAsync(static row => row.GetBool(1)))
            .Should().ContainSingle().Which.Should().BeFalse();
        (await db.GetFundTransactionsAsync(transaction.FundId, transaction.ValueDate, transaction.ValueDate))
            .Should().ContainSingle().Which.Should().Be(canonicalRows[0]);
    }

    [Fact]
    [Trait("detect duplicate canonical fund transaction identities", "FundDb")]
    public async Task FundTransactionBackfill_DuplicateLogicalIdsChooseMinimumAndPreventCutover()
    {
        var db = _testFixture.FundDb;
        var transaction = SampleData.FundTransaction with
        {
            TransactionDate = new DateTime(2048, 6, 20, 9, 8, 7, DateTimeKind.Utc),
            ValueDate = new DateOnly(2048, 6, 20),
            Description = "legacy duplicate"
        };
        await ResetFundTransactionsAsync();

        await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.InsertFundTransaction)}", FundDbCql.InsertFundTransaction)
            .SetParameters(
            [
                new InsertFundTransaction(
                    900,
                    transaction.TransactionDate,
                    transaction.TransactionType.ToString(),
                    transaction.FundId,
                    transaction.OrderId,
                    transaction.TradeId,
                    transaction.TradeType.ToString(),
                    transaction.ValueDate,
                    transaction.TradeStatus.ToString(),
                    transaction.Description,
                    transaction.Amount,
                    transaction.Balance),
                new InsertFundTransaction(
                    100,
                    transaction.TransactionDate,
                    transaction.TransactionType.ToString(),
                    transaction.FundId,
                    transaction.OrderId,
                    transaction.TradeId,
                    transaction.TradeType.ToString(),
                    transaction.ValueDate,
                    transaction.TradeStatus.ToString(),
                    transaction.Description,
                    transaction.Amount,
                    transaction.Balance)
            ])
            .ExecuteCommandAsync();

        var result = await db.BackfillFundTransactionProjectionsAsync(
            transaction.FundId,
            transaction.ValueDate,
            transaction.ValueDate,
            batchSize: 1);

        result.LogicalTransactionKeys.Should().Be(1);
        result.IdentityRows.Should().Be(1);
        result.DuplicateCanonicalRows.Should().Be(1);
        result.ConflictingIdentityRows.Should().Be(0);
        result.CompletedMonths.Should().Be(0);
        result.IsReconciled.Should().BeFalse();
        (await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.GetFundTransactionIdentityV4)}", FundDbCql.GetFundTransactionIdentityV4)
                .SetParameters(new GetFundTransactionIdentityV4(
                    transaction.FundId,
                    transaction.ValueDate,
                    transaction.OrderId,
                    transaction.TradeId,
                    transaction.TradeType.ToString(),
                    transaction.TransactionType.ToString(),
                    transaction.TransactionDate))
                .ExecuteScalarAsync(static row => row.GetLong(0)))
            .Should().Be(100);
    }

    [Fact]
    [Trait("maintain a ready fund projection", "FundDb")]
    public async Task FundTransactionSuccessfulWritePreservesProjectionCutoverAsyncOk()
    {
        var db = _testFixture.FundDb;
        var first = SampleData.FundTransaction with
        {
            TransactionId = 101,
            TransactionDate = new DateTime(2044, 8, 12, 10, 0, 0, DateTimeKind.Utc),
            ValueDate = new DateOnly(2044, 8, 12),
            TransactionType = FundTransactionType.RealizedTradePnl,
            Amount = 10m
        };
        var second = first with
        {
            TransactionId = 102,
            TransactionDate = first.TransactionDate.AddSeconds(1),
            TradeId = first.TradeId + 1,
            Amount = 20m
        };
        await ResetFundTransactionsAsync();
        await db.InsertFundTransactionAsync(first);
        (await db.BackfillFundTransactionProjectionsAsync(
            first.FundId,
            first.ValueDate,
            first.ValueDate,
            batchSize: 1)).IsReconciled.Should().BeTrue();

        await db.InsertFundTransactionAsync(second);
        var updatedSecond = second with
        {
            TradeStatus = TradeStatus.Close,
            Description = "updated maintained projection",
            Amount = -20m
        };
        await db.InsertFundTransactionAsync(updatedSecond);

        var monthBucket = new DateOnly(second.ValueDate.Year, second.ValueDate.Month, 1);
        var states = await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.GetFundTransactionProjectionStateV3)}", FundDbCql.GetFundTransactionProjectionStateV3)
            .SetParameters(new GetFundTransactionProjectionStateV3(second.FundId, monthBucket))
            .ExecuteQueryAsync(row => row.GetBool(1));
        states.Should().ContainSingle().Which.Should().BeTrue();
        var activeMutations = await db.Use($"{nameof(FundDbCql)}.{nameof(FundDbCql.GetFundTransactionProjectionMutationsV3)}", FundDbCql.GetFundTransactionProjectionMutationsV3)
            .SetParameters(new GetFundTransactionProjectionMutationsV3(second.FundId, monthBucket))
            .ExecuteQueryAsync(row => row.GetGuid(0));
        activeMutations.Should().BeEmpty();

        var pnl = await db.GetFundPnlAsync(second.FundId, second.ValueDate, second.ValueDate);
        pnl.Select(row => row.Pnl).Should().BeEquivalentTo(new[] { 10m, -20m });
    }

    [Fact]
    [Trait("return pnl for all trades in fund", "FundDb")]
    public async Task GetFundPnlAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundTransaction.FundId;
        var orderId = SampleData.FundTransaction.OrderId;
        var tradeId = SampleData.FundTransaction.TradeId;
        var valueDate = SampleData.FundTransaction.ValueDate;
        await ResetFundTransactionsAsync();
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TransactionType = FundTransactionType.RealizedTradePnl });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 2, TransactionType = FundTransactionType.RealizedTradePnl });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 3, TransactionType = FundTransactionType.RealizedTradePnl });
        var fundPnl = await db.GetFundPnlAsync(
            SampleData.FundTransaction.FundId,
            SampleData.FundTransaction.ValueDate.AddDays(-1),
            SampleData.FundTransaction.ValueDate.AddDays(1));
        fundPnl.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    [Trait("return current fund balance", "FundDb")]
    public async Task GetFundBalanceAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundTransaction.FundId;
        var valueDate = SampleData.FundTransaction.ValueDate;
        await db
            .UseTest($"delete from fund where fundId = :fundId")
            .SetParameters(new DeleteFund(fundId))
            .ExecuteCommandAsync();
        await db.InsertFundAsync(SampleData.Fund);
        var fundBalance = await db.GetFundBalanceAsync(fundId);
        fundBalance.Should().BeGreaterThanOrEqualTo(SampleData.Fund.Balance);
    }

    [Fact]
    [Trait("return starting fund balance", "FundDb")]
    public async Task GetFundStartingBalanceAsyncOk()
    {
        var db = _testFixture.FundDb;
        var transaction = SampleData.FundTransaction with
        {
            TransactionId = 0,
            TransactionDate = DateTime.UtcNow
        };
        var fundId = transaction.FundId;
        var startDate = transaction.ValueDate.AddDays(-1);
        await ResetFundTransactionsAsync();
        await db.InsertFundTransactionAsync(transaction with { TradeId = 1, Balance = 1000 });
        await db.InsertFundTransactionAsync(transaction with { TradeId = 2, Balance = 2000, TransactionDate = transaction.TransactionDate.AddMilliseconds(1) });
        await db.InsertFundTransactionAsync(transaction with { TradeId = 3, Balance = 3000, TransactionDate = transaction.TransactionDate.AddMilliseconds(2) });
        var fundBalance = await db.GetFundStartingBalanceAsync(fundId, startDate);
        fundBalance.Should().Be(1000);
    }

    [Fact]
    [Trait("return ending fund balance", "FundDb")]
    public async Task GetFundEndingBalanceAsyncOk()
    {
        var db = _testFixture.FundDb;
        var transaction = SampleData.FundTransaction with
        {
            TransactionId = 0,
            TransactionDate = DateTime.UtcNow
        };
        var fundId = transaction.FundId;
        var endDate = transaction.ValueDate.AddDays(1);
        await ResetFundTransactionsAsync();
        await db.InsertFundTransactionAsync(transaction with { TradeId = 1, Balance = 1000 });
        await db.InsertFundTransactionAsync(transaction with { TradeId = 2, Balance = 2000, TransactionDate = transaction.TransactionDate.AddMilliseconds(1) });
        await db.InsertFundTransactionAsync(transaction with { TradeId = 3, Balance = 3000, TransactionDate = transaction.TransactionDate.AddMilliseconds(2) });
        var fundBalacel = await db.GetFundEndingBalanceAsync(fundId, endDate);
        fundBalacel.Should().Be(3000);
    }

    [Fact]
    [Trait("return chronological fund boundary balances", "FundDb")]
    public async Task FundBoundaryBalances_UseValueDateBeforeExplicitTransactionId()
    {
        var db = _testFixture.FundDb;
        var transaction = SampleData.FundTransaction with
        {
            TransactionDate = DateTime.UtcNow,
            ValueDate = new DateOnly(2047, 6, 10)
        };
        var laterValueDate = transaction.ValueDate.AddDays(1);
        await ResetFundTransactionsAsync();
        await db.InsertFundTransactionsAsync(
        [
            transaction with { TransactionId = 900, TradeId = 1, Balance = 111m },
            transaction with
            {
                TransactionId = 800,
                TradeId = 2,
                TransactionDate = transaction.TransactionDate.AddSeconds(10),
                Balance = 112m
            },
            transaction with
            {
                TransactionId = 100,
                TradeId = 3,
                TransactionDate = transaction.TransactionDate.AddSeconds(20),
                ValueDate = laterValueDate,
                Balance = 222m
            },
            transaction with
            {
                TransactionId = 200,
                TradeId = 4,
                TransactionDate = transaction.TransactionDate.AddSeconds(5),
                ValueDate = laterValueDate,
                Balance = 223m
            }
        ]);

        (await db.GetFundStartingBalanceAsync(transaction.FundId, transaction.ValueDate))
            .Should().Be(112m, "the minimum ID remains the tie-breaker inside the first ValueDate");
        (await db.GetFundEndingBalanceAsync(transaction.FundId, laterValueDate))
            .Should().Be(223m, "the maximum ID remains the tie-breaker inside the last ValueDate");
    }

    [Fact]
    [Trait("return fund loss orders", "FundDb")]
    public async Task GetFundLossOrdersAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundTransaction.FundId;
        var startDate = SampleData.FundTransaction.ValueDate.AddDays(-1);
        var endDate = SampleData.FundTransaction.ValueDate.AddDays(1);
        await ResetFundTransactionsAsync();
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 1, Balance = 1000 , Amount = -300, TransactionType = FundTransactionType.RealizedTradePnl});
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 2, Balance = 2000, Amount = -400, TransactionType = FundTransactionType.RealizedTradePnl });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 3, Balance = 3000, Amount = -500, TransactionType = FundTransactionType.RealizedTradePnl });
        var fundLossOrders = await db.GetFundLossOrdersAsync(fundId, startDate, endDate);
        fundLossOrders.Count.Should().Be(1);
        var fundLossOrder = fundLossOrders.First();
        fundLossOrder.Amount.Should().Be(-1200);
    }

    [Fact]
    [Trait("return fund profit orders", "FundDb")]
    public async Task GetFundProfitOrdersAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundTransaction.FundId;
        var startDate = SampleData.FundTransaction.ValueDate.AddDays(-1);
        var endDate = SampleData.FundTransaction.ValueDate.AddDays(1);
        await ResetFundTransactionsAsync();
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 1, Balance = 1000, Amount = 300, TransactionType = FundTransactionType.RealizedTradePnl });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 2, Balance = 2000, Amount = -400, TransactionType = FundTransactionType.RealizedTradePnl });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 3, Balance = 3000, Amount = 500, TransactionType = FundTransactionType.RealizedTradePnl });
        var fundProfitOrders = await db.GetFundProfitOrdersAsync(fundId, startDate, endDate);
        fundProfitOrders.Count.Should().Be(1);
        var fundProfitOrder = fundProfitOrders.First();
        fundProfitOrder.Amount.Should().Be(800);    
    }

    [Fact]
    [Trait("return fund drawdown balances", "FundDb")]
    public async Task GetFundDrawdownBalancesAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundTransaction.FundId;
        var startDate = new DateOnly(2025, 01, 2);
        var endDate = new DateOnly(2025, 01, 28);
        await ResetFundTransactionsAsync();
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 1, Balance = 1000, ValueDate = new DateOnly(2025,01,2), Amount = 300, TransactionType = FundTransactionType.RealizedTradePnl });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 2, Balance = 2000, ValueDate = new DateOnly(2025, 01, 15), Amount = -400, TransactionType = FundTransactionType.RealizedTradePnl });
        await db.InsertFundTransactionAsync(SampleData.FundTransaction with { TradeId = 3, Balance = 3000, ValueDate = new DateOnly(2025, 01, 28), Amount = 500, TransactionType = FundTransactionType.RealizedTradePnl });
        var fundDrawdownBalances = await db.GetFundDrawdownBalancesAsync(fundId, startDate, endDate);
        fundDrawdownBalances.Should().NotBeNull();
        fundDrawdownBalances.FundId.Should().Be(fundId);
        fundDrawdownBalances.StartBalance.Should().Be(1000);
        fundDrawdownBalances.EndBalance.Should().Be(3000);
    }

    [Fact]
    [Trait("return fund daily balances", "FundDb")]
    public async Task UpdatedFundOrderStatusAsyncOk()
    {
        var db = _testFixture.FundDb;
        await db.UseTest($"delete from fund_order where FundId = {SampleData.Fund.FundId} ").ExecuteCommandAsync();
        var oldStatus = SampleData.FundOrder.OrderStatus;
        await db.InsertFundOrderAsync(SampleData.FundOrder);
        await db.UpdateFundOrderStatusAsync(SampleData.FundOrder.FundId, SampleData.FundOrder.OrderId,  Domain.Fund.Shared.OrderStatus.Closed);
        var fundOrders = await db.GetFundOrdersAsync();
        var fundOrder = fundOrders.Where(e => e.FundId == SampleData.FundOrder.FundId && e.OrderId == SampleData.FundOrder.OrderId).SingleOrDefault();
        fundOrder.OrderStatus.Should().NotBe(oldStatus);
        fundOrder.OrderStatus.Should().Be(Domain.Fund.Shared.OrderStatus.Closed);
    }

    [Fact]
    [Trait("reject missing fund order status update", "FundDb")]
    public async Task UpdateFundOrderStatusAsync_MissingOrderDoesNotCreatePartialCanonicalRow()
    {
        var db = _testFixture.FundDb;
        var fundId = Random.Shared.Next(1_000_000_000, 1_400_000_000);
        var orderId = Random.Shared.Next(1_500_000_000, 2_000_000_000);

        await FluentActions.Awaiting(() => db.UpdateFundOrderStatusAsync(
                fundId,
                orderId,
                Domain.Fund.Shared.OrderStatus.Closed))
            .Should().ThrowAsync<global::TomasAI.IFM.Shared.Exceptions.StorageException>();

        (await db.GetFundOrderAsync(fundId, orderId)).Should().BeNull();
    }

    [Fact]
    [Trait("update fund order trade state", "FundDb")]
    public async Task UpdateFundOrderTradeStateAsyncOk()
    {
        var db = _testFixture.FundDb;
        var fundId = SampleData.FundOrderTrade.FundId;
        var orderId = SampleData.FundOrderTrade.OrderId;
        var tradeId = SampleData.FundOrderTrade.TradeId;
        await db.UseTest($"delete from fund_order_trade where FundId = {fundId} and OrderId = {orderId} and TradeId = {tradeId}").ExecuteCommandAsync();
        await db.InsertFundOrderTradeAsync(SampleData.FundOrderTrade);
        var oldState = SampleData.FundOrderTrade.TradeState;
        await db.UpdateFundOrderTradeStateAsync(fundId, orderId, tradeId, global::TomasAI.IFM.Domain.Trade.Shared.TradeState.OrderCompleted, DateTime.Now, "basilt");
        var fundOrderTrades = await db.GetFundOrderTradesAsync();
        var fundOrderTrade = fundOrderTrades.Where(e => e.FundId == fundId && e.OrderId == orderId && e.TradeId == tradeId).SingleOrDefault();
        fundOrderTrade.TradeState.Should().NotBe(oldState);
        fundOrderTrade.TradeState.Should().Be(global::TomasAI.IFM.Domain.Trade.Shared.TradeState.OrderCompleted);
    }

}
