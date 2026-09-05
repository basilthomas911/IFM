using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.MarketData.UnitTests.Harness;
using TomasAI.IFM.Application.MarketData.Worker;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class SupervisedHostPublisherLifecycleTests
{
    static readonly DateOnly ValueDate = new(2026, 9, 4);
    const string ContractId = "ES20261218";

    [Fact]
    public async Task Admitted_output_without_starting_the_host_publisher_reproduces_the_original_failure()
    {
        var actorSupervisor = Substitute.For<IActorSupervisor>();
        actorSupervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());
        await using var publisher = new TickAggregationEventPublisher(actorSupervisor);
        var admissions = new DatasetWorkerAdmissionRegistry();
        var identity = new DatasetWorkerAdmission("GLBX.MDP3", ValueDate, Guid.NewGuid(), Guid.NewGuid(), 1);
        admissions.Admit(identity);
        var ingress = new DatasetPublicationIngress(admissions, publisher, Substitute.For<IMarketDataOperationsRecorder>());
        var envelope = new DatasetPublicationEnvelope
        {
            Dataset = identity.Dataset, ValueDate = identity.ValueDate, WorkerInstanceId = identity.WorkerInstanceId,
            GenerationId = identity.GenerationId, ManifestRevision = 1, PublicationSequence = 1,
            Kind = DatasetPublicationKind.MarketPrice,
            Payload = MessagePackSerializer.Serialize(new FuturesMarketPriceUpdatedRealtimeEvent
            {
                Price = new FuturesMarketPriceSnapshot(ContractId, 42, 1, AssetTypeId.Futures, ValueDate, null, null)
            })
        };

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ingress.AcceptAsync(envelope));

        Assert.Contains("publisher is not running", failure.Message);
    }

    [Fact]
    public async Task Real_host_publisher_starts_before_worker_admission_and_delivers_to_actor_and_api()
    {
        await using var fixture = new Fixture();
        Assert.False(fixture.Publisher.IsRunning);
        await fixture.Runtime.StartAsync(ValueDate, CancellationToken.None);

        var published = await fixture.DeliveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.True(fixture.Publisher.IsRunning);
        Assert.Equal(ContractId, published.Price.ContractId);
        Assert.True(fixture.Api.TryGetLastTickPrice(ContractId, out var price));
        Assert.Equal(ContractId, price.ContractId);
        var reader = fixture.Api.GetFuturesLastPriceReader(ContractId);
        Assert.True(reader.TryGetLastTrade(out _) || reader.TryGetLastQuote(out _));
        Assert.Equal(ValueDate, fixture.Api.GetRuntimeStatus().ActiveValueDate);
        Assert.Equal(0, fixture.EpochFactory.CreateCount);
        Assert.True(fixture.Admissions.TryGet("GLBX.MDP3", out _));

        await fixture.Runtime.StopAsync(CancellationToken.None);

        Assert.False(fixture.Publisher.IsRunning);
        Assert.False(fixture.Admissions.TryGet("GLBX.MDP3", out _));
        Assert.All(fixture.Children, child => Assert.False(child.Current.Running));
        Assert.False(reader.TryGetLastTrade(out _));
        Assert.False(reader.TryGetLastQuote(out _));
        Assert.False(fixture.Api.GetRuntimeStatus().IsRunning);
        Assert.Null(fixture.Runtime.ActiveValueDate);
        await fixture.Producer.DidNotReceive().StartAsync(Arg.Any<ActorMailboxId>());
        await fixture.Producer.DidNotReceive().StopAsync();
    }

    [Fact]
    public async Task Failure_after_host_publisher_start_rolls_back_its_lifecycle()
    {
        await using var fixture = new Fixture(failWorkerLaunch: true);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Runtime.StartAsync(ValueDate, CancellationToken.None));

        Assert.Equal("Injected worker launch failure.", exception.Message);
        Assert.False(fixture.Publisher.IsRunning);
        Assert.Empty(fixture.Workers.Current);
        Assert.False(fixture.Admissions.TryGet("GLBX.MDP3", out _));
        Assert.Null(fixture.Runtime.ActiveValueDate);
        Assert.False(fixture.Api.GetRuntimeStatus().IsRunning);
    }

    [Fact]
    public async Task Noncooperative_actor_send_cannot_block_worker_shutdown_or_allow_unsafe_session_restart()
    {
        await using var fixture = new Fixture(blockActorSend: true);
        await fixture.Runtime.StartAsync(ValueDate, CancellationToken.None);
        await fixture.DeliveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));

        var stop = fixture.Runtime.StopAsync(CancellationToken.None);
        await Assert.ThrowsAsync<AggregateException>(() => stop.WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.All(fixture.Children, child => Assert.False(child.Current.Running));
        Assert.False(fixture.Admissions.TryGet("GLBX.MDP3", out _));
        Assert.False(fixture.Api.GetRuntimeStatus().IsRunning);
        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Runtime.StartAsync(ValueDate, CancellationToken.None));
        Assert.Contains("restart the host", blocked.Message);
        fixture.ReleaseDelivery.TrySetResult();
    }

    sealed class Fixture : IAsyncDisposable
    {
        public IActorProducer Producer { get; } = Substitute.For<IActorProducer>();
        public TaskCompletionSource<FuturesMarketPriceUpdatedRealtimeEvent> DeliveryStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDelivery { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<DatasetWorkerProcessSupervisor> Children { get; } = [];
        public DatasetWorkerAdmissionRegistry Admissions { get; } = new();
        public DatasetWorkerCurrentValues Values { get; } = new();
        public FakeMarketDataEpochFactory EpochFactory { get; } = new(new FakeMarketDataCatalog());
        public TickAggregationEventPublisher Publisher { get; }
        public DatasetWorkerProcessRecoveryService Workers { get; }
        public SupervisedDatabentoLifecycleRuntime Runtime { get; }
        public DatabentoMarketDataApi Api { get; }

        public Fixture(bool failWorkerLaunch = false, bool blockActorSend = false)
        {
            var actorSupervisor = Substitute.For<IActorSupervisor>();
            actorSupervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Producer);
            Producer.SendAsync<FuturesMarketPriceUpdatedRealtimeEvent, TickDataEntityId>(
                Arg.Any<ActorSubject>(), Arg.Any<FuturesMarketPriceUpdatedRealtimeEvent>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    DeliveryStarted.TrySetResult(call.Arg<FuturesMarketPriceUpdatedRealtimeEvent>());
                    return blockActorSend ? new ValueTask(ReleaseDelivery.Task) : ValueTask.CompletedTask;
                });
            Publisher = new TickAggregationEventPublisher(actorSupervisor);
            var ingress = new DatasetPublicationIngress(Admissions, Publisher,
                Substitute.For<IMarketDataOperationsRecorder>(), Values);
            var processOptions = new DatabentoStage3Options
            {
                WorkerHandshakeTimeout = TimeSpan.FromSeconds(10), WorkerStartTimeout = TimeSpan.FromSeconds(15),
                WorkerCommandTimeout = TimeSpan.FromSeconds(5), WorkerGracefulStopTimeout = TimeSpan.FromSeconds(2),
                WorkerForceKillTimeout = TimeSpan.FromSeconds(5)
            };
            Workers = new DatasetWorkerProcessRecoveryService(processOptions, Admissions, ingress,
                supervisorFactory: options =>
                {
                    Assert.True(Publisher.IsRunning); // Must precede worker launch/admission.
                    if (failWorkerLaunch) throw new InvalidOperationException("Injected worker launch failure.");
                    var child = new DatasetWorkerProcessSupervisor(options,
                        async (publication, token) => { await ingress.AcceptAsync(publication, token); });
                    Children.Add(child);
                    return child;
                }, currentValues: Values);
            var options = new DatabentoMarketDataRuntimeOptions
            {
                FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.SyntheticCi, "GLBX.MDP3"),
                Contracts = [new DatabentoContractRegistration
                {
                    DomainContractId = ContractId, ProviderContractName = "ESZ6", AssetTypeId = AssetTypeId.Futures,
                    Dataset = "GLBX.MDP3", RootSymbol = "ES", OnTheRun = true, Rollover = true
                }]
            };
            var registry = new DatabentoContractRegistrationRegistry(options.Contracts, options);
            Runtime = new SupervisedDatabentoLifecycleRuntime(Substitute.For<IDatabentoContractAuthority>(),
                registry, Workers, new DatabentoSupervisedWorkerOptions
                {
                    DotNetHostPath = DotNetHost(),
                    WorkerAssemblyPath = typeof(DatasetWorkerAssemblyMarker).Assembly.Location,
                    HostPublisherStopTimeout = TimeSpan.FromMilliseconds(300),
                    Synthetic = new SyntheticFeedOptions { RecordCount = 1_000_000, RecordsPerSecond = 50 }
                }, TimeProvider.System, Publisher);
            Api = new DatabentoMarketDataApi(EpochFactory, new DatabentoMarketDataApiOptions(), currentValues: Values);
        }

        public async ValueTask DisposeAsync()
        {
            ReleaseDelivery.TrySetResult();
            await Runtime.StopAsync(CancellationToken.None);
            await Workers.DisposeAsync();
            await Publisher.DisposeAsync();
            await Api.DisposeAsync();
            Values.Dispose();
        }

        static string DotNetHost()
        {
            var configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
            var candidate = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!,
                OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            return File.Exists(candidate) ? candidate : throw new InvalidOperationException("dotnet host was not found.");
        }
    }
}
