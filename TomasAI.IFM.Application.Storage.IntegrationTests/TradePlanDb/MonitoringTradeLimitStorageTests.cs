using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class MonitoringTradeLimitStorageTests(PostgresTradePlanFixture fixture)
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Every_insert_overload_preserves_the_negative_currency_loss_limit(int overload)
    {
        var connection = fixture.Connection;
        var settings = new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection, connection, "System.Data.Postgres");
        var logger = Substitute.For<ILogger<DbProvider>>();
        await new TradeSchemaDb(settings, logger).CreateAllAsync();
        var factory = Substitute.For<IDbContextFactory>();
        var database = new TradeDbContext(settings, factory, Substitute.For<ISequenceIdGenerator>(), logger);
        factory.TradeDb.Returns(database);
        var limits = new TradeLimitReadModel { TradeId = Random.Shared.Next(4000000,5000000), TradeType = TradeType.ShortIronCondor,
            RiskMargin = 5000, MaxProfit = 1300, MaxLoss = -2000, MaxReturn = .26m, MaxLossLimit = 26,
            MinProfitLimit = 3.25m, MaxProfitLimit = 3.25m, MinProfitTarget = 660.4m, DailyProfitTarget = 140.4m,
            CreatedOn = DateTime.UtcNow, CreatedBy = "monitoring-verification", UpdatedOn = DateTime.UtcNow, UpdatedBy = "monitoring-verification" };
        switch (overload)
        {
            case 0: await database.InsertTradeLimitAsync(limits); break;
            case 1: await database.InsertTradeLimitsAsync((ICollection<TradeLimitReadModel>)new[] { limits }); break;
            default: (await database.InsertTradeLimitsAsync((IEnumerable<TradeLimitReadModel>)new[] { limits })).Should().Be(1); break;
        }
        (await database.GetTradeLimitAsync(-limits.TradeId)).Should().BeNull();
        var stored = await database.GetTradeLimitAsync(limits.TradeId);
        stored!.MaxLoss.Should().Be(-2000); stored.MaxProfit.Should().Be(1300); stored.MaxLossLimit.Should().Be(26);
    }
}
