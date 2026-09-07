using MessagePack;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.UnitTests.Harness;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class DatasetWorkerCurrentValuesTests
{
    static readonly DateOnly ValueDate = new(2026, 9, 4);
    static readonly DateTimeOffset Now = new(2026, 9, 4, 15, 0, 0, TimeSpan.Zero);
    const string Es = "ES20260918";
    const string Vx = "VX20260916";

    [Fact]
    public void Contract_health_is_independent_of_other_datasets_and_fenced_by_generation()
    {
        using var values = new DatasetWorkerCurrentValues();
        var es = Admission("GLBX.MDP3"); var vx = Admission("XCBF.PITCH");
        values.ActivateDataset(es, [Registration(Es, es.Dataset)]);
        values.ActivateDataset(vx, [Registration(Vx, vx.Dataset)]);
        values.SetDatasetHealth(es, true);
        Assert.False(values.IsFeedUp);
        var first = values.GetFuturesMarketHealth(Es);
        Assert.True(first.Healthy); Assert.False(values.GetFuturesMarketHealth(Vx).Healthy);
        Assert.False(values.GetFuturesMarketHealth("missing").Healthy);
        values.ClearDataset(es.Dataset);
        Assert.False(values.GetFuturesMarketHealth(Es).Healthy);
        var next = es with { GenerationId = Guid.NewGuid() };
        values.ActivateDataset(next, [Registration(Es, next.Dataset)]);
        values.SetDatasetHealth(es, true);
        Assert.False(values.GetFuturesMarketHealth(Es).Healthy);
        values.SetDatasetHealth(next, true);
        Assert.True(values.GetFuturesMarketHealth(Es).Healthy);
        Assert.NotEqual(first.Generation, values.GetFuturesMarketHealth(Es).Generation);
    }

    [Fact]
    public void Retained_readers_clear_on_reset_then_receive_the_replacement_generation()
    {
        using var values = new DatasetWorkerCurrentValues();
        var es = Admission("GLBX.MDP3");
        var vx = Admission("XCBF.PITCH");
        values.ActivateDataset(es, [Registration(Es, es.Dataset)]);
        values.ActivateDataset(vx, [Registration(Vx, vx.Dataset)]);
        var esReader = values.GetFuturesReader(Es);
        var vxReader = values.GetFuturesReader(Vx);
        Assert.True(values.AcceptPublication(Price(es, Es, 6500m)));
        Assert.True(values.AcceptPublication(Price(vx, Vx, 20m)));

        values.ClearDataset(es.Dataset);

        Assert.False(esReader.TryGetLastTrade(out _));
        Assert.False(esReader.TryGetLastQuote(out _));
        Assert.False(values.TryGetLastTickPrice(Es, out _));
        Assert.True(vxReader.TryGetLastTrade(out var retained));
        Assert.Equal(20m, retained.Price);
        Assert.False(values.AcceptPublication(Price(es, Es, 6501m, 2)));
        var replacement = es with { GenerationId = Guid.NewGuid() };
        values.ActivateDataset(replacement, [Registration(Es, es.Dataset)]);
        Assert.Same(esReader, values.GetFuturesReader(Es));
        Assert.Same(vxReader, values.GetFuturesReader(Vx));
        Assert.True(values.AcceptPublication(Price(replacement, Es, 6502m)));
        Assert.True(esReader.TryGetLastTrade(out var recovered));
        Assert.Equal(6502m, recovered.Price);
        Assert.False(values.AcceptPublication(Price(es, Es, 9999m, 3)));
    }

    [Fact]
    public void Admission_identity_membership_date_and_sequence_are_enforced()
    {
        using var values = new DatasetWorkerCurrentValues();
        var es = Admission("GLBX.MDP3");
        values.ActivateDataset(es, [Registration(Es, es.Dataset)]);
        Assert.False(values.AcceptPublication(Price(es with { ManifestRevision = 2 }, Es, 1m)));
        Assert.False(values.AcceptPublication(Price(es with { WorkerInstanceId = Guid.NewGuid() }, Es, 1m)));
        Assert.False(values.AcceptPublication(Price(es, Vx, 1m)));
        Assert.False(values.AcceptPublication(Price(es, Es, 1m) with { ValueDate = ValueDate.AddDays(1) }));
        Assert.True(values.AcceptPublication(Price(es, Es, 6500m)));
        Assert.False(values.AcceptPublication(Price(es, Es, 9999m)));
        Assert.True(values.GetFuturesReader(Es).TryGetLastTrade(out var trade));
        Assert.Equal(6500m, trade.Price);
    }

    [Fact]
    public void Statistics_are_dataset_scoped_and_cleared_with_its_prices()
    {
        using var values = new DatasetWorkerCurrentValues();
        var es = Admission("GLBX.MDP3");
        var vx = Admission("XCBF.PITCH");
        values.ActivateDataset(es, [Registration(Es, es.Dataset)]);
        values.ActivateDataset(vx, [Registration(Vx, vx.Dataset)]);
        Assert.True(values.AcceptPublication(Statistics(es, Es)));
        Assert.True(values.AcceptPublication(Statistics(vx, Vx)));
        Assert.True(values.TryGetFuturesSessionStatistics(Es, out _));
        values.ClearDataset(es.Dataset);
        Assert.False(values.TryGetFuturesSessionStatistics(Es, out _));
        Assert.True(values.TryGetFuturesSessionStatistics(Vx, out _));
    }

    [Fact]
    public void Changed_manifest_removes_old_values_without_affecting_other_dataset()
    {
        using var values = new DatasetWorkerCurrentValues();
        var es = Admission("GLBX.MDP3");
        var vx = Admission("XCBF.PITCH");
        values.ActivateDataset(es, [Registration(Es, es.Dataset)]);
        values.ActivateDataset(vx, [Registration(Vx, vx.Dataset)]);
        var removed = values.GetFuturesReader(Es);
        var retained = values.GetFuturesReader(Vx);
        values.AcceptPublication(Price(es, Es, 6500m));
        values.AcceptPublication(Price(vx, Vx, 20m));
        var next = es with { ManifestRevision = 2, GenerationId = Guid.NewGuid() };
        values.ActivateDataset(next, [Registration("ES20261218", es.Dataset)]);
        Assert.False(removed.TryGetLastTrade(out _));
        Assert.Throws<MarketDataContractNotFoundException>(() => values.GetFuturesReader(Es));
        Assert.True(retained.TryGetLastTrade(out _));
        Assert.True(values.AcceptPublication(Price(next, "ES20261218", 6600m)));
    }

    [Fact]
    public void Stop_invalidates_retained_readers_and_next_value_date_gets_new_readers()
    {
        using var values = new DatasetWorkerCurrentValues();
        var es = Admission("GLBX.MDP3");
        values.ActivateDataset(es, [Registration(Es, es.Dataset)]);
        var reader = values.GetFuturesReader(Es);
        values.AcceptPublication(Price(es, Es, 6500m));
        values.Stop();
        Assert.Null(values.ActiveValueDate);
        Assert.False(reader.TryGetLastTrade(out _));
        Assert.False(values.AcceptPublication(Price(es, Es, 6501m, 2)));
        values.ActivateDataset(es with { ValueDate = ValueDate.AddDays(1) }, [Registration(Es, es.Dataset)]);
        Assert.NotSame(reader, values.GetFuturesReader(Es));
        Assert.Equal(ValueDate.AddDays(1), values.GetFuturesReader(Es).ValueDate);
    }

    [Fact]
    public void Capacity_is_bounded_and_immutable_manifest_updates_fail_without_partial_clear()
    {
        using var values = new DatasetWorkerCurrentValues(capacity: 1);
        var es = Admission("GLBX.MDP3");
        values.ActivateDataset(es, [Registration(Es, es.Dataset)]);
        values.AcceptPublication(Price(es, Es, 6500m));
        Assert.Throws<ArgumentException>(() => values.ActivateDataset(es,
            [Registration(Es, es.Dataset) with { ProviderContractName = "OTHER" }]));
        Assert.Throws<InvalidOperationException>(() => values.ActivateDataset(
            es with { ManifestRevision = 2 }, [Registration("ES20261218", es.Dataset)]));
        Assert.True(values.GetFuturesReader(Es).TryGetLastTrade(out var trade));
        Assert.Equal(6500m, trade.Price);
    }

    [Fact]
    public void Stale_probe_cannot_requalify_a_replacement_worker()
    {
        using var values = new DatasetWorkerCurrentValues();
        var es = Admission("GLBX.MDP3");
        values.ActivateDataset(es, [Registration(Es, es.Dataset)]);
        values.SetDatasetHealth(es, true);
        Assert.True(values.IsFeedUp);
        values.ClearDataset(es.Dataset);
        var replacement = es with { GenerationId = Guid.NewGuid() };
        values.ActivateDataset(replacement, [Registration(Es, es.Dataset)]);
        values.SetDatasetHealth(es, true);
        Assert.False(values.IsFeedUp);
        values.SetDatasetHealth(replacement, true);
        Assert.True(values.IsFeedUp);
    }

    [Fact]
    public async Task Late_publication_racing_clear_cannot_repopulate_retained_handles()
    {
        using var values = new DatasetWorkerCurrentValues();
        var es = Admission("GLBX.MDP3");
        values.ActivateDataset(es, [Registration(Es, es.Dataset)]);
        var reader = values.GetFuturesReader(Es);
        var envelopes = Enumerable.Range(1, 256).Select(sequence => Price(es, Es, sequence, sequence)).ToArray();
        await Task.WhenAll(
            Task.Run(() => { foreach (var envelope in envelopes) values.AcceptPublication(envelope); }),
            Task.Run(() => values.ClearDataset(es.Dataset)));
        Assert.False(reader.TryGetLastTrade(out _));
        Assert.False(reader.TryGetLastQuote(out _));
        Assert.False(values.TryGetLastTickPrice(Es, out _));
    }

    [Fact]
    public async Task Supervised_freshness_retains_existing_trade_then_midpoint_rules()
    {
        using var values = new DatasetWorkerCurrentValues();
        var es = Admission("GLBX.MDP3");
        values.ActivateDataset(es, [Registration(Es, es.Dataset)]);
        var factory = new FakeMarketDataEpochFactory(new FakeMarketDataCatalog());
        await using var api = new DatabentoMarketDataApi(factory,
            new DatabentoMarketDataApiOptions { MaximumLastPriceAge = TimeSpan.FromSeconds(5) },
            new FixedTimeProvider(Now), currentValues: values);
        var price = new FuturesMarketPriceSnapshot(Es, 42, 1, AssetTypeId.Futures, ValueDate,
            new FuturesMarketQuoteSnapshot(6499m, 1, 6501m, 2, 1, 1, 1, Now, Now),
            new FuturesMarketTradeSnapshot(6000m, 1, 1, Now.AddSeconds(-10), Now));
        Assert.True(values.AcceptPublication(Envelope(es, DatasetPublicationKind.MarketPrice, 1,
            new FuturesMarketPriceUpdatedRealtimeEvent { Price = price })));
        Assert.Equal(6500m, await api.GetFuturesPriceAsync(Es));
        var stale = price with { Quote = price.Quote!.Value with { EventTimestamp = Now.AddSeconds(-10), SourceSequence = 2 } };
        Assert.True(values.AcceptPublication(Envelope(es, DatasetPublicationKind.MarketPrice, 2,
            new FuturesMarketPriceUpdatedRealtimeEvent { Price = stale })));
        Assert.Null(await api.GetFuturesPriceAsync(Es));
    }

    [Fact]
    public async Task Supervised_api_reads_prices_without_creating_an_in_process_epoch()
    {
        using var values = new DatasetWorkerCurrentValues();
        var es = Admission("GLBX.MDP3");
        values.ActivateDataset(es, [Registration(Es, es.Dataset)]);
        var factory = new FakeMarketDataEpochFactory(new FakeMarketDataCatalog());
        await using var api = new DatabentoMarketDataApi(factory, new DatabentoMarketDataApiOptions(),
            new FixedTimeProvider(Now), currentValues: values);
        values.AcceptPublication(Price(es, Es, 6500m));
        values.AcceptPublication(Statistics(es, Es, 2));
        Assert.Equal(6500m, await api.GetFuturesPriceAsync(Es));
        Assert.True(api.TryGetLastTickPrice(Es, out _));
        Assert.True(api.TryGetFuturesSessionStatistics(Es, out _));
        Assert.Same(api.GetFuturesLastPriceReader(Es), api.GetFuturesLastPriceReader(Es));
        Assert.True(api.GetRuntimeStatus().IsRunning);
        Assert.Equal(ValueDate, api.ActiveValueDate);
        Assert.False(api.IsDatabentoFeedUp());
        values.SetDatasetHealth(es, true);
        Assert.True(api.IsDatabentoFeedUp());
        values.ClearDataset(es.Dataset);
        Assert.False(api.IsDatabentoFeedUp());
        Assert.Null(await api.GetFuturesPriceAsync(Es));
        Assert.Equal(0, factory.CreateCount);
    }

    [Fact]
    public async Task Supervised_reference_queries_use_the_catalog_but_transient_routes_fail_explicitly()
    {
        var context = new MarketDataApiTestContext();
        using var values = new DatasetWorkerCurrentValues();
        var es = Admission("GLBX.MDP3");
        values.ActivateDataset(es, [Registration(MarketDataApiTestContext.FutureId, es.Dataset)], context.Catalog);
        await using var api = new DatabentoMarketDataApi(context.EpochFactory,
            new DatabentoMarketDataApiOptions(), currentValues: values);
        var contract = await api.GetFuturesContractAsync(MarketDataApiTestContext.FutureId);
        Assert.Equal(context.Catalog.Futures[MarketDataApiTestContext.FutureId], contract);
        var batch = await api.GetFuturesContractsAsync([MarketDataApiTestContext.FutureId, MarketDataApiTestContext.FutureId]);
        Assert.Equal(2, batch.Length);
        Assert.Equal(batch[0], batch[1]);
        Assert.Null(await api.GetFuturesContractAsync("MISSING"));
        Assert.Throws<NotSupportedException>(() => { _ = api.StartStreamingFuturesTickDataAsync(MarketDataApiTestContext.FutureId); });
        Assert.Throws<NotSupportedException>(() => { _ = api.StartStreamingFuturesOptionChainDataAsync(
            MarketDataApiTestContext.FutureId, MarketDataApiTestContext.OptionMaturity, [MarketDataApiTestContext.CallId]); });
        Assert.Throws<NotSupportedException>(() => api.TryGetLastOptionTickPrice(MarketDataApiTestContext.CallId, out _));
        Assert.False(api.IsTickDataStreamActive(MarketDataApiTestContext.FutureId));
        Assert.Equal(0, context.EpochFactory.CreateCount);
    }

    [Fact]
    public async Task Supervised_catalog_returns_authoritative_front_and_back_metadata_without_fabrication()
    {
        var options = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
            Contracts = []
        };
        var registry = new DatabentoContractRegistrationRegistry([], options);
        var es = Contract(Es, "ES", "ESU6", new DateOnly(2026, 9, 18));
        var front = Contract(Vx, "VX", "VXU6", new DateOnly(2026, 9, 16));
        var back = Contract("VX20261021", "VX", "VXV6", new DateOnly(2026, 10, 21)) with { OnTheRun = false };
        registry.ReplaceFuturesRolloverSet("ES", [es]);
        registry.ReplaceFuturesRolloverSet("VX", [front, back]);
        using var values = new DatasetWorkerCurrentValues(registry);
        foreach (var dataset in registry.Snapshot().GroupBy(value => value.Dataset!))
            values.ActivateDataset(Admission(dataset.Key), dataset.ToArray());
        var factory = new FakeMarketDataEpochFactory(new FakeMarketDataCatalog());
        await using var api = new DatabentoMarketDataApi(factory, new DatabentoMarketDataApiOptions(),
            contractRegistry: registry, currentValues: values);
        Assert.Same(es, await api.GetFuturesContractAsync(Es));
        Assert.Same(front, await api.GetFuturesContractAsync(Vx));
        Assert.Same(back, await api.GetFuturesContractAsync(back.ContractId));
        Assert.Equal([back, es, front, es], await api.GetFuturesContractsAsync([back.ContractId, Es, Vx, Es]));
        Assert.Equal(0, factory.CreateCount);
    }

    [Fact]
    public async Task Missing_metadata_is_explicit_and_missing_contract_keeps_existing_null_semantics()
    {
        using var values = new DatasetWorkerCurrentValues();
        var es = Admission("GLBX.MDP3");
        values.ActivateDataset(es, [Registration(Es, es.Dataset)]);
        var factory = new FakeMarketDataEpochFactory(new FakeMarketDataCatalog());
        await using var api = new DatabentoMarketDataApi(factory, new DatabentoMarketDataApiOptions(), currentValues: values);
        Assert.Throws<MarketDataContractMappingException>(() => { _ = api.GetFuturesContractAsync(Es); });
        Assert.Null(await api.GetFuturesContractAsync("MISSING"));
        values.Stop();
        Assert.False(api.GetRuntimeStatus().IsRunning);
        Assert.False(api.GetHealth().Running);
        Assert.Null(api.ActiveValueDate);
        Assert.Throws<MarketDataApiNotRunningException>(() => api.GetFuturesLastPriceReader(Es));
        await Assert.ThrowsAsync<NotSupportedException>(() => api.StartAsync(ValueDate));
        Assert.Equal(0, factory.CreateCount);
    }

    [Fact]
    public async Task Status_is_coherent_while_a_session_restarts()
    {
        using var values = new DatasetWorkerCurrentValues();
        var factory = new FakeMarketDataEpochFactory(new FakeMarketDataCatalog());
        await using var api = new DatabentoMarketDataApi(factory, new DatabentoMarketDataApiOptions(), currentValues: values);
        var es = Admission("GLBX.MDP3");
        await Task.WhenAll(Task.Run(() =>
        {
            for (var index = 0; index < 256; index++)
            {
                values.ActivateDataset(es, [Registration(Es, es.Dataset)]);
                values.Stop();
            }
        }), Task.Run(() =>
        {
            for (var index = 0; index < 1024; index++)
            {
                var status = api.GetRuntimeStatus();
                Assert.Equal(status.IsRunning, status.ActiveValueDate.HasValue);
                var health = api.GetHealth();
                Assert.Equal(health.Running, health.ValueDate.HasValue);
            }
        }));
        Assert.False(values.GetStatus().IsFeedUp);
    }

    static FuturesContractV3ReadModel Contract(string id, string symbol, string localSymbol, DateOnly date) => new(
        id, $"Authoritative {id}", symbol, localSymbol, "FUT", "USD", symbol == "VX" ? "CFE" : "CME",
        symbol == "VX" ? "1000" : "50", date, true, true);

    static DatasetWorkerAdmission Admission(string dataset) =>
        new(dataset, ValueDate, Guid.NewGuid(), Guid.NewGuid(), 1);

    static DatabentoContractRegistration Registration(string id, string dataset) => new()
    {
        DomainContractId = id, Dataset = dataset, ProviderContractName = id,
        AssetTypeId = AssetTypeId.Futures
    };

    static DatasetPublicationEnvelope Price(DatasetWorkerAdmission identity, string id, decimal price, long sequence = 1)
        => Envelope(identity, DatasetPublicationKind.MarketPrice, sequence,
            new FuturesMarketPriceUpdatedRealtimeEvent
            {
                Price = new FuturesMarketPriceSnapshot(id, 42, 1, AssetTypeId.Futures, identity.ValueDate,
                    new FuturesMarketQuoteSnapshot(price - .25m, 1, price + .25m, 2, 1, 1, sequence, Now, Now),
                    new FuturesMarketTradeSnapshot(price, 1, sequence, Now, Now)),
                UpdateSource = FuturesMarketPriceUpdateSource.Trade
            });

    static DatasetPublicationEnvelope Statistics(DatasetWorkerAdmission identity, string id, long sequence = 1)
        => Envelope(identity, DatasetPublicationKind.SessionStatistics, sequence,
            new FuturesSessionStatisticsUpdatedRealtimeEvent
            {
                Statistics = new FuturesSessionStatisticsSnapshot(id, identity.ValueDate, 100m, 110m, 90m, 1, 1)
            });

    static DatasetPublicationEnvelope Envelope<T>(DatasetWorkerAdmission identity, DatasetPublicationKind kind,
        long sequence, T payload) => new()
    {
        Dataset = identity.Dataset, ValueDate = identity.ValueDate, WorkerInstanceId = identity.WorkerInstanceId,
        GenerationId = identity.GenerationId, ManifestRevision = identity.ManifestRevision,
        PublicationSequence = sequence, Kind = kind, Payload = MessagePackSerializer.Serialize(payload)
    };

    sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
