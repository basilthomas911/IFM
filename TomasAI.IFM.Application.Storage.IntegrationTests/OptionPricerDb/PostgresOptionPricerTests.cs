using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Npgsql;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.OptionPricerDb;

public sealed class PostgresOptionPricerTests
{
    const string Connection = "Host=localhost;Port=5432;Database=ifm_storage_refactor_test";
    static async Task<OptionPricerDbContext> Context()
    {
        var settings = new DbConnectionSettings().Add(OptionPricerDbContext.ConnectionName, Connection, "System.Data.Postgres");
        await new OptionPricerSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        var factory = Substitute.For<IDbContextFactory>();
        var context = new OptionPricerDbContext(settings, factory, Substitute.For<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);
        factory.OptionPricerDb.Returns(context);
        return context;
    }

    static SpreadDistributionReadModel Distribution(int trade, long id, TradeType type) => new(
        id, trade, new DateOnly(2026, 10, 11), type, TradeStatus.Open, 30,
        12.125, 0.16, 1234.567890123456789m, 12, 0.21, 0.2, 0.85, DateTime.UtcNow);

    [Fact]
    public async Task Devices_upsert_and_delete_through_the_existing_context_contract()
    {
        var context = await Context(); var id = Random.Shared.Next(10_000_000, 20_000_000);
        var device = new OptionPricerDeviceReadModel(id, "pg-" + id, 1000, 2000, 64, OptionType.Put, true);
        await context.InsertOptionPricerDeviceAsync(device);
        await context.InsertOptionPricerDeviceAsync(device with { MaxBatchSize = 128 });
        (await context.GetOptionPricerDevicesAsync()).Single(x => x.DeviceId == id).MaxBatchSize.Should().Be(128);
        await context.DeleteOptionPricerDeviceAsync(device.EntityId);
        (await context.GetOptionPricerDevicesAsync()).Should().NotContain(x => x.DeviceId == id);
    }

    [Fact]
    public async Task Distribution_pair_preserves_decimal_precision_and_reads_latest_revision()
    {
        var context = await Context(); var trade = Random.Shared.Next(10_000_000, 20_000_000);
        var put = Distribution(trade, 1, TradeType.PutCreditSpread);
        var call = Distribution(trade, 2, TradeType.CallCreditSpread);
        await context.InsertSpreadDistributionsAsync(put, call);
        await context.InsertSpreadDistributionsAsync(put with { Id = 3, ForwardPrice = 13 }, call);
        var result = await context.GetSpreadDistributionAsync(trade, put.TradeType, put.TradeStatus, put.ValueDate, put.DaysToExpiry);
        result!.Id.Should().Be(3); result.ForwardPrice.Should().Be(13); result.LossThreshold.Should().Be(put.LossThreshold);
        await context.DeleteSpreadDistributionAsync(trade, put.ValueDate);
    }

    [Fact]
    public async Task Failure_in_second_distribution_rolls_back_the_first_distribution()
    {
        var context = await Context(); var trade = Random.Shared.Next(20_000_000, 30_000_000);
        var put = Distribution(trade, 1, TradeType.PutCreditSpread);
        var call = Distribution(trade + 1, 2, TradeType.CallCreditSpread);
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(Connection);
        await connection.OpenAsync();
        var constraint = "test_pair_" + Guid.NewGuid().ToString("N");
        await using (var command = new NpgsqlCommand($"ALTER TABLE option_pricer.spread_distribution ADD CONSTRAINT {constraint} CHECK (tradeId <> {call.TradeId});", connection))
            await command.ExecuteNonQueryAsync();
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => context.InsertSpreadDistributionsAsync(put, call));
            (await context.GetSpreadDistributionAsync(trade, put.TradeType, put.TradeStatus, put.ValueDate, put.DaysToExpiry)).Should().BeNull();
        }
        finally
        {
            await using var command = new NpgsqlCommand($"ALTER TABLE option_pricer.spread_distribution DROP CONSTRAINT {constraint};", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Jobs_preserve_null_timestamps_and_change_status_without_duplicates()
    {
        var context = await Context(); var id = Random.Shared.Next(10_000_000, 20_000_000); var date = new DateOnly(2026, 10, 11);
        var job = new SpreadDistributionJobReadModel
        {
            OrderId = id, TradeId = id, ValueDate = date, TradeType = TradeType.PutCreditSpread,
            TradeStatus = TradeStatus.Open, DaysToExpiry = 30, JobSubmitted = DateTime.UtcNow,
            JobStatus = SpreadDistributionJobStatus.InProgress, InProgress = true, LossProbabilityFactor = 1
        };
        await context.InsertSpreadDistributionJobAsync(job);
        await context.InsertSpreadDistributionJobAsync(job);
        (await context.GetSpreadDistributionJobInProgressCountAsync(id, id)).Should().Be(1);
        await context.UpdateSpreadDistributionJobStatusAsync(id, id, date, SpreadDistributionJobStatus.Completed, DateTime.UtcNow, null);
        (await context.GetSpreadDistributionJobInProgressCountAsync(id, id)).Should().Be(0);
        await context.DeleteSpreadDistributionJobsAsync(id, id);
    }
}
