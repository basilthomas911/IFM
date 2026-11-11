using TomasAI.IFM.Shared.MarketData.Definitions;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.TradeStrategySymbols;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using Xunit;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class InstrumentDefinitionRefreshTests
{
    public static ExactInstrumentDefinition Definition(uint id, string raw, string asset, string kind, uint underlying = 0,
        string exchange = "XCME", ulong received = 1, string action = "A", ushort publisher = 1, string currency = "USD")
        => ExactInstrumentDefinition.Parse("GLBX.MDP3", JsonSerializer.Serialize(new
        {
            hd = new { publisher_id = publisher, instrument_id = id, rtype = 19, ts_event = "1788089407203111341" },
            ts_recv = received.ToString(),
            raw_symbol = raw,
            asset,
            instrument_class = kind,
            underlying_id = underlying,
            underlying = "",
            currency,
            exchange,
            security_update_action = action,
            expiration = "1947591000000000000",
            activation = "1692394200000000000",
            strike_price = "2020000000000",
            high_limit_price = "9223372036854775807",
            custom_provider_field = "preserved"
        }));
    static DatabentoMarketDataRuntimeOptions Options() => new()
    {
        Contracts = [],
        FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development, "GLBX.MDP3")
    };
    static InstrumentDefinitionRefresh Refresh(IInstrumentDefinitionProvider provider, IInstrumentDefinitionStore store)
        => new(provider, store, Options(), TimeProvider.System, NullLogger<InstrumentDefinitionRefresh>.Instance);
    public static async IAsyncEnumerable<ExactInstrumentDefinition> Stream(ExactInstrumentDefinition[] rows,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var row in rows) { cancellationToken.ThrowIfCancellationRequested(); yield return row; await Task.Yield(); }
    }

    [Fact]
    public void Full_provider_fields_and_integer_precision_are_preserved()
    {
        var row = Definition(42, "LOZ6 P7000", "LO", "P", 12);
        using var json = JsonDocument.Parse(row.Json);
        Assert.Equal("9223372036854775807", json.RootElement.GetProperty("high_limit_price").GetString());
        Assert.Equal("preserved", json.RootElement.GetProperty("custom_provider_field").GetString());
        Assert.Equal(12u, row.Summary.UnderlyingInstrumentId);
        Assert.Equal(ContractKind.PutOption, row.Summary.ContractKind);
    }

    [Fact]
    public async Task Saves_every_exact_record_and_publishes_distinct_current_underlying_products_after_writes()
    {
        var rows = new[] { Definition(1, "CLZ6", "CL", "F", exchange: "XNYM"),
            Definition(2, "LOZ6 C7000", "LO", "C", 1, "XNYM"), Definition(3, "LOZ6 P7000", "LO", "P", 1, "XNYM"),
            Definition(4, "OLDZ6", "OLD", "F"), Definition(4, "OLDZ6", "OLD", "F", received: 2, action: "D"),
            Definition(5, "UNRESOLVED", "BAD", "P", 999), Definition(6, "WRONGVENUE", "BAD", "P", 1, publisher: 2),
            Definition(7, "SPREAD", "SPREAD", "S"), Definition(8, "BADUSD", "BAD", "F", currency: "") };
        var provider = Substitute.For<IInstrumentDefinitionProvider>();
        provider.ReadLatestAsync("GLBX.MDP3", Arg.Any<CancellationToken>()).Returns(Stream(rows));
        var store = Substitute.For<IInstrumentDefinitionStore>();
        var writes = 0;
        store.InsertAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<ExactInstrumentDefinition>(), Arg.Any<CancellationToken>())
            .Returns(_ => { Interlocked.Increment(ref writes); return Task.CompletedTask; });
        IReadOnlyCollection<TradeStrategyProduct>? published = null;
        store.PublishAsync(Arg.Any<InstrumentDefinitionSnapshot>(), Arg.Any<IReadOnlyCollection<TradeStrategyProduct>>(), Arg.Any<CancellationToken>())
            .Returns(call => { Assert.Equal(rows.Length, writes); published = call.Arg<IReadOnlyCollection<TradeStrategyProduct>>(); return Task.CompletedTask; });
        var snapshot = await Refresh(provider, store).RefreshAsync();
        Assert.Equal(rows.Length, snapshot.RecordCount);
        Assert.Equal(3, published!.Count);
        Assert.Contains(new TradeStrategyProduct(TradeStrategyFamilyType.Futures, "CL", "USD", "XNYM"), published);
        Assert.Contains(new TradeStrategyProduct(TradeStrategyFamilyType.FuturesOption, "CL", "USD", "XNYM"), published);
        Assert.Contains(new TradeStrategyProduct(TradeStrategyFamilyType.FuturesOption, "CL", "USD", "XCME"), published);
    }

    [Fact]
    public async Task Cme_and_cfe_publish_together_and_a_failed_second_dataset_keeps_previous_catalog()
    {
        var provider = Substitute.For<IInstrumentDefinitionProvider>();
        provider.ReadLatestAsync("GLBX.MDP3", Arg.Any<CancellationToken>()).Returns(Stream([Definition(1,"ESZ6","ES","F")]));
        var vx = ExactInstrumentDefinition.Parse("XCBF.PITCH", Definition(2,"VX/X6","VX","F",exchange:"XCBF",publisher:105).Json);
        var ux = ExactInstrumentDefinition.Parse("XCBF.PITCH", Definition(3,"UX3C/X6 C2000","UX","C",underlying:2,exchange:"XCBF",publisher:105).Json);
        provider.ReadLatestAsync("XCBF.PITCH", Arg.Any<CancellationToken>()).Returns(Stream([vx,ux]));
        var store = Substitute.For<IInstrumentDefinitionStore>();
        using var refresh = new InstrumentDefinitionRefresh(provider,store,Options() with {TradeStrategySymbolDatasets=["GLBX.MDP3","XCBF.PITCH"]},TimeProvider.System);
        var result = await refresh.RefreshAsync();
        Assert.Equal(new[]{"GLBX.MDP3","XCBF.PITCH"},result.Datasets);
        await store.Received(1).PublishAsync(Arg.Any<InstrumentDefinitionSnapshot>(),Arg.Is<IReadOnlyCollection<TradeStrategyProduct>>(p=>p.Any(x=>x.Symbol=="ES") && p.Any(x=>x.Symbol=="VX" && x.Exchange=="XCBF" && x.Family==TradeStrategyFamilyType.FuturesOption)),Arg.Any<CancellationToken>());
        store.ClearReceivedCalls();
        provider.ReadLatestAsync("XCBF.PITCH", Arg.Any<CancellationToken>()).Returns(Stream([]));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>refresh.RefreshAsync());
        await store.DidNotReceive().PublishAsync(Arg.Any<InstrumentDefinitionSnapshot>(),Arg.Any<IReadOnlyCollection<TradeStrategyProduct>>(),Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Bulk_ingest_preserves_all_records_and_never_publishes_after_batch_failure()
    {
        var provider = Substitute.For<IInstrumentDefinitionProvider>();
        var rows = Enumerable.Range(1,129).Select(i=>Definition((uint)i,$"ES{i}","ES","F")).ToArray();
        provider.ReadLatestAsync("GLBX.MDP3",Arg.Any<CancellationToken>()).Returns(Stream(rows));
        var store = Substitute.For<IInstrumentDefinitionStore,IInstrumentDefinitionBatchStore>();
        var bulk = (IInstrumentDefinitionBatchStore)store;
        List<(long First,ExactInstrumentDefinition[] Rows)> received=[];
        bulk.InsertBatchAsync(Arg.Any<Guid>(),Arg.Any<long>(),Arg.Any<IReadOnlyList<ExactInstrumentDefinition>>(),Arg.Any<CancellationToken>())
            .Returns(call=>{received.Add((call.Arg<long>(),call.Arg<IReadOnlyList<ExactInstrumentDefinition>>().ToArray()));return Task.CompletedTask;});
        await Refresh(provider,store).RefreshAsync();
        Assert.Equal(new long[]{0,128},received.Select(x=>x.First));
        Assert.Equal(rows.Select(x=>x.Json),received.SelectMany(x=>x.Rows).Select(x=>x.Json));
        await store.DidNotReceive().InsertAsync(Arg.Any<Guid>(),Arg.Any<long>(),Arg.Any<ExactInstrumentDefinition>(),Arg.Any<CancellationToken>());
        store.ClearReceivedCalls();
        bulk.InsertBatchAsync(Arg.Any<Guid>(),Arg.Any<long>(),Arg.Any<IReadOnlyList<ExactInstrumentDefinition>>(),Arg.Any<CancellationToken>()).Returns(Task.FromException(new IOException("batch failed")));
        await Assert.ThrowsAsync<IOException>(()=>Refresh(provider,store).RefreshAsync());
        await store.DidNotReceive().PublishAsync(Arg.Any<InstrumentDefinitionSnapshot>(),Arg.Any<IReadOnlyCollection<TradeStrategyProduct>>(),Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Failed_write_does_not_publish_a_partial_snapshot()
    {
        var provider = Substitute.For<IInstrumentDefinitionProvider>();
        provider.ReadLatestAsync("GLBX.MDP3", Arg.Any<CancellationToken>()).Returns(Stream([Definition(1, "ESZ6", "ES", "F")]));
        var store = Substitute.For<IInstrumentDefinitionStore>();
        store.InsertAsync(Arg.Any<Guid>(), Arg.Any<long>(), Arg.Any<ExactInstrumentDefinition>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new IOException("Scylla unavailable")));
        await Assert.ThrowsAsync<IOException>(() => Refresh(provider, store).RefreshAsync());
        await store.DidNotReceive().PublishAsync(Arg.Any<InstrumentDefinitionSnapshot>(), Arg.Any<IReadOnlyCollection<TradeStrategyProduct>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Query_uses_the_current_database_snapshot_and_observes_refresh_immediately()
    {
        var store = Substitute.For<IInstrumentDefinitionStore>();
        var first = new InstrumentDefinitionSnapshot(Guid.NewGuid(), DateTime.UtcNow, 100, ["GLBX.MDP3"]);
        var second = first with { Id = Guid.NewGuid() };
        store.GetSnapshotAsync(Arg.Any<CancellationToken>()).Returns(first, second);
        var es = new TradeStrategyProduct(TradeStrategyFamilyType.FuturesOption, "ES", "USD", "XCME").WithId(101);
        var cl = new TradeStrategyProduct(TradeStrategyFamilyType.FuturesOption, "CL", "USD", "XNYM").WithId(102);
        store.GetSymbolsAsync(first.Id, TradeStrategyFamilyType.FuturesOption, Arg.Any<CancellationToken>()).Returns([es]);
        store.GetSymbolsAsync(second.Id, TradeStrategyFamilyType.FuturesOption, Arg.Any<CancellationToken>()).Returns([cl]);
        var catalog = new StoredInstrumentDefinitionSymbolCatalog(store);
        Assert.Equal(es, Assert.Single((await catalog.GetAsync(TradeStrategyFamilyType.FuturesOption)).Value!));
        Assert.Equal(cl, Assert.Single((await catalog.GetAsync(TradeStrategyFamilyType.FuturesOption)).Value!));
    }

    [Fact]
    public async Task Missing_snapshot_is_reported_and_cancelled_refresh_never_publishes()
    {
        var store = Substitute.For<IInstrumentDefinitionStore>();
        var result = await new StoredInstrumentDefinitionSymbolCatalog(store).GetAsync(TradeStrategyFamilyType.Futures);
        Assert.False(result.Success); Assert.Equal(503, result.ErrorCode);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Refresh(Substitute.For<IInstrumentDefinitionProvider>(), store).RefreshAsync(cancellation.Token));
        await store.DidNotReceive().PublishAsync(Arg.Any<InstrumentDefinitionSnapshot>(), Arg.Any<IReadOnlyCollection<TradeStrategyProduct>>(), Arg.Any<CancellationToken>());
    }
}
