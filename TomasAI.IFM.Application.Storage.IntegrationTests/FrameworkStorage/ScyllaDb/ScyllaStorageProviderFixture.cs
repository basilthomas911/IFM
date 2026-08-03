using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.FundDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage.ScyllaDb;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ScyllaStorageProviderCollection : ICollectionFixture<ScyllaStorageProviderFixture>
{
    public const string Name = "Framework.Storage ScyllaDB integration";
}

public sealed class ScyllaStorageProviderFixture : IAsyncLifetime
{
    const string ConnectionVariable = "IFM_SCYLLA_TEST_CONNECTION";
    const string ProviderName = "System.Data.ScyllaDb";

    const string DeleteFund = "DELETE FROM fund WHERE fundId = :fundId;";
    const string DeleteFundOrders = "DELETE FROM fund_order WHERE fundId = :fundId;";
    const string DeleteFundOrderTrades = "DELETE FROM fund_order_trade WHERE fundId = :fundId AND orderId = :orderId;";
    const string DeleteFundTransactions = "DELETE FROM fund_transaction WHERE fundId = :fundId;";

    const string CountFunds = "SELECT count(*) FROM fund WHERE fundId = :fundId;";
    const string CountFundOrders = "SELECT count(*) FROM fund_order WHERE fundId = :fundId;";
    const string CountFundOrderTrades = "SELECT count(*) FROM fund_order_trade WHERE fundId = :fundId AND orderId = :orderId;";
    const string CountFundTransactions = "SELECT count(*) FROM fund_transaction WHERE fundId = :fundId;";

    readonly ILogger<DbProvider> _logger = Substitute.For<ILogger<DbProvider>>();

    public ScyllaTestRepository Repository { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set {ConnectionVariable} to a credential-free ScyllaDB connection string whose default keyspace is dedicated to integration tests.");
        }

        var settings = new DbConnectionSettings()
            .Add(FundDbContext.FundDbConnection, connectionString, ProviderName);

        await new FundSchemaDb(settings, _logger).CreateAllAsync();
        Repository = new ScyllaTestRepository(settings[FundDbContext.FundDbConnection], _logger);

        await CleanupAllScopesAsync();
    }

    public async Task DisposeAsync()
    {
        if (Repository is null)
            return;

        await CleanupAllScopesAsync();
    }

    internal async Task RunIsolatedAsync(ScyllaFundTestScope scope, Func<ScyllaTestRepository, Task> test)
    {
        await CleanupAndVerifyAsync(scope);
        try
        {
            await test(Repository);
        }
        finally
        {
            await CleanupAndVerifyAsync(scope);
        }
    }

    async Task CleanupAllScopesAsync()
    {
        for (var slot = 1; slot <= ScyllaFundTestData.SlotCount; slot++)
            await CleanupAndVerifyAsync(ScyllaFundTestData.Scope(slot));
    }

    async Task CleanupAndVerifyAsync(ScyllaFundTestScope scope)
    {
        await Repository.Use(DeleteFundTransactions)
            .SetParameters(new FundKey(scope.FundId))
            .ExecuteCommandAsync();

        foreach (var orderId in scope.OrderIds)
        {
            await Repository.Use(DeleteFundOrderTrades)
                .SetParameters(new FundOrderKey(scope.FundId, orderId))
                .ExecuteCommandAsync();
        }

        await Repository.Use(DeleteFundOrders)
            .SetParameters(new FundKey(scope.FundId))
            .ExecuteCommandAsync();

        await Repository.Use(DeleteFund)
            .SetParameters(new FundKey(scope.FundId))
            .ExecuteCommandAsync();

        await EnsureEmptyAsync(CountFundTransactions, new FundKey(scope.FundId), "fund_transaction");
        foreach (var orderId in scope.OrderIds)
            await EnsureEmptyAsync(CountFundOrderTrades, new FundOrderKey(scope.FundId, orderId), "fund_order_trade");
        await EnsureEmptyAsync(CountFundOrders, new FundKey(scope.FundId), "fund_order");
        await EnsureEmptyAsync(CountFunds, new FundKey(scope.FundId), "fund");
    }

    async Task EnsureEmptyAsync<TParam>(string cql, TParam parameters, string table)
        where TParam : struct, IBindValue
    {
        var count = await Repository.Use(cql)
            .SetParameters(parameters)
            .ExecuteScalarAsync(static row => row.GetLong(0));

        if (count != 0)
            throw new InvalidOperationException($"Scylla integration cleanup left {count} row(s) in {table}.");
    }

    readonly record struct FundKey(int fundId) : IBindValue
    {
        public object Bind() => new object?[] { fundId };
    }

    readonly record struct FundOrderKey(int fundId, int orderId) : IBindValue
    {
        public object Bind() => new object?[] { fundId, orderId };
    }
}

public sealed class ScyllaTestRepository(
    IDbConnectionSetting connectionSetting,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<ScyllaTestRepository>(connectionSetting, logger)
{
    public override IObjectRepository Database => this;
}
