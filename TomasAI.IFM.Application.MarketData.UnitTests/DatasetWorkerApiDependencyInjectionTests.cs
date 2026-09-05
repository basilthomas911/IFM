using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.UnitTests.Harness;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

/// <summary>Uses the production registration extension and real DI constructor selection.</summary>
public sealed class DatasetWorkerApiDependencyInjectionTests
{
    static readonly DateOnly ValueDate = new(2026, 9, 4);
    static readonly DateTimeOffset Now = new(2026, 9, 4, 16, 0, 0, TimeSpan.Zero);
    const string ContractId = "ES20260918";
    const string Dataset = "GLBX.MDP3";

    [Fact]
    public async Task Legacy_registration_resolves_one_api_without_mirror_and_retains_epoch_lifecycle()
    {
        var services = Services(out var factory);
        services.AddApplicationMarketDataApi(Options());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        var api = provider.GetRequiredService<IMarketDataApi>();

        Assert.Same(provider.GetRequiredService<DatabentoMarketDataApi>(), api);
        Assert.Null(provider.GetService<DatasetWorkerCurrentValues>());
        Assert.False(api.GetRuntimeStatus().IsRunning);
        Assert.False(api.TryGetLastTickPrice(ContractId, out _));
        Assert.Equal(0, factory.CreateCount);

        await api.StartAsync(ValueDate);
        var epoch = Assert.Single(factory.Epochs);
        epoch.LastMarketPrice = Price();
        Assert.Equal(1, factory.CreateCount);
        Assert.True(api.GetRuntimeStatus().IsRunning);
        Assert.Equal(ValueDate, api.GetRuntimeStatus().ActiveValueDate);
        Assert.True(api.TryGetLastTickPrice(ContractId, out var observed));
        Assert.Equal(Price(), observed);

        await api.StopAsync(ValueDate);
        Assert.False(api.GetRuntimeStatus().IsRunning);
        Assert.False(api.TryGetLastTickPrice(ContractId, out _));
        Assert.Equal(1, epoch.StopCount);
        Assert.Equal(1, epoch.DisposeCount);
    }

    [Theory]
    [InlineData(false)] // Startup adds the optional mirror after AddApplicationMarketDataApi.
    [InlineData(true)]
    public async Task Supervised_registration_injects_the_same_host_mirror_without_creating_an_epoch(
        bool registerMirrorFirst)
    {
        var services = Services(out var factory);
        if (registerMirrorFirst) services.AddSingleton<DatasetWorkerCurrentValues>();
        services.AddApplicationMarketDataApi(Options());
        if (!registerMirrorFirst) services.AddSingleton<DatasetWorkerCurrentValues>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        var api = provider.GetRequiredService<IMarketDataApi>();
        var concrete = provider.GetRequiredService<DatabentoMarketDataApi>();
        var values = provider.GetRequiredService<DatasetWorkerCurrentValues>();
        Assert.Same(concrete, api);
        Assert.Same(values, provider.GetRequiredService<DatasetWorkerCurrentValues>());
        Assert.False(api.GetRuntimeStatus().IsRunning);
        Assert.False(api.TryGetLastTickPrice(ContractId, out _));

        var registry = provider.GetRequiredService<IDatabentoContractRegistrationRegistry>();
        var contract = new FuturesContractV3ReadModel(ContractId, "Authoritative ES", "ES", "ESU6",
            "FUT", "USD", "CME", "50", new DateOnly(2026, 9, 18), true, true);
        registry.ReplaceFuturesRolloverSet("ES", [contract]);
        var admission = new DatasetWorkerAdmission(Dataset, ValueDate, Guid.NewGuid(), Guid.NewGuid(), 1);
        values.ActivateDataset(admission, registry.Snapshot());
        values.SetDatasetHealth(admission, true);
        Assert.True(values.AcceptPublication(Envelope(admission, 1, DatasetPublicationKind.MarketPrice,
            new FuturesMarketPriceUpdatedRealtimeEvent { Price = Price() })));
        var statistics = new FuturesSessionStatisticsSnapshot(ContractId, ValueDate, 6500m, 6520m, 6480m, 1, 1);
        Assert.True(values.AcceptPublication(Envelope(admission, 2, DatasetPublicationKind.SessionStatistics,
            new FuturesSessionStatisticsUpdatedRealtimeEvent { Statistics = statistics })));

        Assert.True(api.TryGetLastTickPrice(ContractId, out var observed));
        Assert.Equal(Price(), observed);
        Assert.True(api.TryGetFuturesSessionStatistics(ContractId, out var observedStatistics));
        Assert.Equal(statistics, observedStatistics);
        var reader = api.GetFuturesLastPriceReader(ContractId);
        Assert.Same(values.GetFuturesReader(ContractId), reader);
        Assert.Equal(6500m, await api.GetFuturesPriceAsync(ContractId));
        Assert.Same(contract, await api.GetFuturesContractAsync(ContractId));
        Assert.True(api.IsDatabentoFeedUp());
        Assert.True(api.GetRuntimeStatus().IsRunning);
        Assert.True(concrete.GetHealth().Running);
        Assert.Equal(ValueDate, concrete.ActiveValueDate);
        Assert.Equal(ValueDate, api.GetRuntimeStatus().ActiveValueDate);
        Assert.Equal(Now, api.GetRuntimeStatus().ObservedAtUtc);

        values.ClearDataset(Dataset);
        Assert.False(api.TryGetLastTickPrice(ContractId, out _));
        Assert.False(api.TryGetFuturesSessionStatistics(ContractId, out _));
        Assert.False(reader.TryGetLastTrade(out _));
        Assert.False(api.IsDatabentoFeedUp());
        values.Stop();
        Assert.False(api.GetRuntimeStatus().IsRunning);
        Assert.Null(concrete.ActiveValueDate);
        Assert.Equal(0, factory.CreateCount);
        Assert.Empty(factory.Epochs);
    }

    static ServiceCollection Services(out FakeMarketDataEpochFactory factory)
    {
        var services = new ServiceCollection();
        factory = new FakeMarketDataEpochFactory(new FakeMarketDataCatalog());
        services.AddSingleton<IDatabentoMarketDataEpochFactory>(factory);
        services.AddSingleton(Substitute.For<IDatabentoCurrentFuturesContractResolver>());
        services.AddSingleton(Substitute.For<IFuturesContractRolloverStore>());
        services.AddSingleton<TimeProvider>(new FixedTimeProvider());
        return services;
    }

    static DatabentoMarketDataRuntimeOptions Options() => new()
    {
        FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.SyntheticCi, Dataset),
        Contracts = [new DatabentoContractRegistration
        {
            DomainContractId = ContractId,
            ProviderContractName = "ESU6",
            AssetTypeId = AssetTypeId.Futures,
            Dataset = Dataset,
            RootSymbol = "ES",
            OnTheRun = true,
            Rollover = true
        }]
    };

    static FuturesMarketPriceSnapshot Price() => new(ContractId, 42, 1, AssetTypeId.Futures, ValueDate,
        new FuturesMarketQuoteSnapshot(6499m, 1, 6501m, 1, 1, 1, 1, Now, Now),
        new FuturesMarketTradeSnapshot(6500m, 2, 1, Now, Now));

    static DatasetPublicationEnvelope Envelope<T>(DatasetWorkerAdmission identity, long sequence,
        DatasetPublicationKind kind, T value) => new()
    {
        Dataset = identity.Dataset, ValueDate = identity.ValueDate,
        WorkerInstanceId = identity.WorkerInstanceId, GenerationId = identity.GenerationId,
        ManifestRevision = identity.ManifestRevision, PublicationSequence = sequence,
        Kind = kind, Payload = MessagePackSerializer.Serialize(value)
    };

    sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
