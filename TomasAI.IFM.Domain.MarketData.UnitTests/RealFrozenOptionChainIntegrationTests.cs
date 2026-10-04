using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class RealFrozenOptionChainIntegrationTests
{
    [Fact]
    public async Task Loads_current_ES_expiry_from_real_definitions_EOD_and_bulk_quote_table()
    {
        if (Environment.GetEnvironmentVariable("IFM_REAL_SCYLLA_TEST") != "1") return;
        var settings = new DbConnectionSettings()
            .Add("MarketDataDbConnection",
                "Contact Points=localhost;Port=9042;Default Keyspace=market_data_test_db", "System.Data.ScyllaDb")
            .Add("SecuritiesDbConnection",
                "Contact Points=localhost;Port=9042;Default Keyspace=securities_test_db", "System.Data.ScyllaDb");
        var factory = Substitute.For<IDbContextFactory>();
        var databaseLogger = Substitute.For<ILogger<DbProvider>>();
        var marketData = new MarketDataDbContext(settings, factory,
            Substitute.For<IBlackboardService>(), Substitute.For<ISequenceIdGenerator>(), databaseLogger);
        var securities = new SecuritiesDbContext(settings, factory, databaseLogger);
        factory.MarketDataDb.Returns(marketData);
        factory.SecuritiesDb.Returns(securities);
        var context = new MarketDataQueryContext(Substitute.For<IActorSupervisor>(), factory,
            Substitute.For<ILogger<MarketDataQueryActor>>(),
            new FuturesMarketSessionAuthority(TimeProvider.System));

        var query = new GetEvaluatedOptionChainQuery
        {
            UnderlyingContractId = "ES20261218", UnderlyingSymbol = "ES",
            ProviderRoots = ["E1A"], ExpiryDate = new DateOnly(2026, 10, 5),
            StandardDeviationMultiplier = 2.5
        };
        var persistedQuotes = await marketData.GetFuturesOptionChainQuoteDataAsync(query.UnderlyingContractId, query.ExpiryDate, new DateOnly(2026, 10, 2));
        Assert.NotEmpty(persistedQuotes);
        for (var sample = 1; sample <= 5; sample++)
        {
            var timer = Stopwatch.StartNew();
            var result = await FrozenEmulatorOptionChain.ExecuteAsync(query, context, CancellationToken.None);
            timer.Stop();
            Assert.True(result.Success, result.ErrorMessage);
            Assert.NotNull(result.Value);
            Assert.True(result.Value.Contracts.Length > 0);
            foreach (var evaluated in result.Value.Contracts)
            {
                var quote = Assert.Single(persistedQuotes, x => x.ContractId == evaluated.ContractId);
                Assert.Equal((decimal)quote.BidPrice, evaluated.Bid);
                Assert.Equal((decimal)quote.AskPrice, evaluated.Ask);
                Assert.Equal(quote.Volume, evaluated.Volume);
                Assert.Equal(quote.OpenInterest, evaluated.OpenInterest);
                Assert.Equal(quote.VolumeValueDate, evaluated.VolumeValueDate);
                Assert.Equal(quote.OpenInterestValueDate, evaluated.OpenInterestValueDate);
                Assert.True(evaluated.Delta.HasValue);
                Assert.True(evaluated.GreeksValid);
                Assert.True(evaluated.Vega.HasValue);
            }
            var realQuoteCount = result.Value.Contracts.Count(x => persistedQuotes.Any(q => q.ContractId == x.ContractId));
            Assert.Equal(result.Value.Contracts.Length, realQuoteCount);
            Console.WriteLine($"Historical quote coverage: {realQuoteCount}/{result.Value.Contracts.Length}; remaining contracts use model fallback.");
            Assert.Contains(result.Value.Contracts, contract => contract.Delta is > 0 and < 1);
            Assert.Contains(result.Value.Contracts, contract => contract.Delta is < 0 and > -1);
            Console.WriteLine($"Real ES frozen chain sample {sample}: {result.Value.Contracts.Length} contracts in {timer.Elapsed.TotalMilliseconds:F1} ms");
            Assert.True(timer.Elapsed < TimeSpan.FromSeconds(2),
                $"Real frozen chain exceeded 2 seconds: {timer.Elapsed.TotalMilliseconds:F1} ms");
        }
        var enrichedQuotes = await marketData.GetFuturesOptionChainQuoteDataAsync(query.UnderlyingContractId, query.ExpiryDate, new DateOnly(2026, 10, 2));
        Assert.Contains(enrichedQuotes, quote => quote.Vega > 0);
        foreach (var quote in enrichedQuotes)
        {
            var original = Assert.Single(persistedQuotes, x => x.ContractId == quote.ContractId);
            Assert.Equal(original.BidPrice, quote.BidPrice);
            Assert.Equal(original.AskPrice, quote.AskPrice);
            Assert.Equal(original.TickId, quote.TickId);
        }
    }
}





