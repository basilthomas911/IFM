using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Extensions;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class RealOptionChainQuoteDataTests
{
    [Fact]
    public void Historical_quote_enrichment_calculates_full_Greeks_before_storage_without_changing_prices()
    {
        var observedAt = new DateTimeOffset(2026, 10, 2, 20, 0, 0, TimeSpan.Zero);
        var definition = new FuturesOptionContractReadModel
        {
            ContractId = "ES20261005C5000", StrikePrice = 5000, OptionType = "Call",
            ExpirationUtc = observedAt.AddDays(7)
        };
        var source = new FuturesOptionTickDataV2ReadModel
        {
            ContractId = definition.ContractId, UnderlyingPrice = 5000, BidPrice = 78.25, AskPrice = 79.25,
            ValueDate = new(2026, 10, 2), TickId = (observedAt - DateTimeOffset.UnixEpoch).Ticks * 100,
            TickTime = new(20, 0), BidSize = 4, AskSize = 5
        };
        var value = OptionQuoteGreekEnrichment.Calculate(definition, source);
        Assert.True(value.Vega > 0); Assert.True(value.ImpliedVolatility > 0);
        Assert.True(value.Delta is > 0 and < 1);
        Assert.Equal(source.BidPrice, value.BidPrice); Assert.Equal(source.AskPrice, value.AskPrice);
        Assert.Equal(source.TickId, value.TickId); Assert.Equal(source.TickTime, value.TickTime);
    }

    [Fact]
    public async Task Persisted_option_tick_event_updates_bulk_quote_projection_on_real_scylla()
    {
        if (Environment.GetEnvironmentVariable("IFM_REAL_SCYLLA_TEST") != "1") return;
        var connection = new DbConnectionSettings().Add("MarketDataDbConnection",
            "Contact Points=localhost;Port=9042;Default Keyspace=market_data_test_db", "System.Data.ScyllaDb");
        var factory = Substitute.For<IDbContextFactory>();
        var db = new MarketDataDbContext(connection, factory,
            Substitute.For<IBlackboardService>(), Substitute.For<ISequenceIdGenerator>(),
            Substitute.For<ILogger<DbProvider>>());
        factory.MarketDataDb.Returns(db);
        var actor = Substitute.For<IFuturesOptionTickDataCommandContext>();
        actor.DbFactory.Returns(factory);
        actor.DbEventSource.Returns(Substitute.For<IEventSourceActorDbContext>());
        actor.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        actor.BlackboardService.Returns(Substitute.For<IBlackboardService>());
        var projector = new FuturesOptionTickDataEventProjector(actor,
            Substitute.For<ILogger<FuturesOptionTickDataEventProjector>>());
        var descriptor = projector.ProjectionDescriptors.Single(item =>
            item.SourceEventType == typeof(FuturesOptionTickDataInsertedEvent));
        var underlying = $"OPTIONCHAININTEGRATION{Guid.NewGuid():N}";
        var date = new DateOnly(2000, 1, 1);
        var tick = new FuturesOptionTickDataV2ReadModel
        {
            ContractId = "ES20261005C5000", ValueDate = date,
            TickId = 654321, BidPrice = 13, AskPrice = 13.25
        };
        var source = new FuturesOptionTickDataInsertedEvent
        {
            Contract = new FuturesContractV3ReadModel { ContractId = underlying }, TickData = tick
        };
        var context = new ProjectionExecutionContext(projector.ProjectorName, tick.TickId, 42,
            new EventProjectorEffectIdentity(projector.ProjectorName, tick.TickId,
                EventProjectorEffectKind.TargetProjection), Guid.NewGuid(),
            EventProjectionIdempotencyStrategy.NaturalKeyMutation, CancellationToken.None, 7);
        try
        {
            await descriptor.ApplyAsync(source, context);
            var rows = await db.GetFuturesOptionChainQuoteDataAsync(underlying,
                new DateOnly(2026, 10, 5), date);
            Assert.Single(rows);
            Assert.Equal(13, rows.Single().BidPrice);
        }
        finally
        {
            await db.UseTest($"DELETE FROM futures_option_chain_quote_data WHERE underlyingContractId = '{underlying}' AND expiryDate = '2026-10-05' AND valueDate = '2000-01-01'")
                .ExecuteCommandAsync();
            await db.UseTest("DELETE FROM futures_option_tick_data WHERE contractId = 'ES20261005C5000' AND valueDate = '2000-01-01'")
                .ExecuteCommandAsync();
        }
    }

    [Fact]
    public async Task Bulk_quote_round_trip_on_development_scylla()
    {
        if (Environment.GetEnvironmentVariable("IFM_REAL_SCYLLA_TEST") != "1") return;
        var connection = new DbConnectionSettings().Add("MarketDataDbConnection",
            "Contact Points=localhost;Port=9042;Default Keyspace=market_data_test_db", "System.Data.ScyllaDb");
        var factory = Substitute.For<IDbContextFactory>();
        var db = new MarketDataDbContext(connection, factory,
            Substitute.For<IBlackboardService>(), Substitute.For<ISequenceIdGenerator>(),
            Substitute.For<ILogger<DbProvider>>());
        factory.MarketDataDb.Returns(db);
        var underlying = $"OPTIONCHAININTEGRATION{Guid.NewGuid():N}";
        var date = new DateOnly(2026, 10, 2);
        var expiry = new DateOnly(2026, 10, 5);
        try
        {
            // These IDs are published in the real ES option-definition cache.
            var call = new FuturesOptionTickDataV2ReadModel
            {
                ContractId = "ES20261005C5000", ValueDate = date,
                TickId = 101, BidPrice = 10, AskPrice = 10.25
            };
            var put = call with { ContractId = "ES20261005P5000", BidPrice = 11, AskPrice = 11.25 };
            await db.UpsertFuturesOptionChainQuoteDataAsync(underlying, call);
            await db.UpsertFuturesOptionChainQuoteDataAsync(underlying, put);
            await db.UpsertFuturesOptionChainQuoteDataAsync(underlying,
                call with { TickId = 102, BidPrice = 12, AskPrice = 12.25 });

            var rows = await db.GetFuturesOptionChainQuoteDataAsync(underlying, expiry, date);
            Assert.Equal(2, rows.Count);
            Assert.Equal(102, rows.Single(row => row.ContractId == call.ContractId).TickId);
            Assert.Equal(12, rows.Single(row => row.ContractId == call.ContractId).BidPrice);
            Assert.Equal(11, rows.Single(row => row.ContractId == put.ContractId).BidPrice);
            Assert.Empty(await db.GetFuturesOptionChainQuoteDataAsync(underlying, expiry.AddDays(1), date));
            await db.UpdateFuturesOptionChainQuoteGreeksAsync(underlying, expiry,
                [call with { TickId = 102, Vega = 123, Delta = .5 }, put with { Vega = 45 }]);
            await db.UpdateFuturesOptionChainQuoteGreeksAsync(underlying, expiry,
                [call with { Vega = 999 }, call with { ContractId = "ES20261005C9999", Vega = 999 }]);
            rows = await db.GetFuturesOptionChainQuoteDataAsync(underlying, expiry, date);
            Assert.Equal(2, rows.Count); // Conditional enrichment cannot create missing quote rows.
            Assert.Equal(123, rows.Single(row => row.ContractId == call.ContractId).Vega);
            Assert.Equal(45, rows.Single(row => row.ContractId == put.ContractId).Vega);
            Assert.Equal(12, rows.Single(row => row.ContractId == call.ContractId).BidPrice);
            Assert.Equal(102, rows.Single(row => row.ContractId == call.ContractId).TickId);

        }
        finally
        {
            await db.UseTest($"DELETE FROM futures_option_chain_quote_data WHERE underlyingContractId = '{underlying}' AND expiryDate = '2026-10-05' AND valueDate = '2026-10-02'")
                .ExecuteCommandAsync();
        }
    }
}
