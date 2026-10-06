using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SimpleInjector;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Command.Actor;
using TomasAI.IFM.Domain.BrokerAccount.Event.Actor;
using TomasAI.IFM.Domain.BrokerAccount.Query.Actor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Command.Actor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Event.Actor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Query.Actor;
using TomasAI.IFM.Domain.Trade.Order.Broker.Realtime.Actor;
using TomasAI.IFM.Framework.TradeBroker.Contracts;
using TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Application.Actor.IntegrationTests;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.TradeFlow;

/// <summary>Verifies the isolated emulator composition without running a strategy workflow.</summary>
public sealed class TradeBrokerEmulatorRegistrationTests
{
    /// <summary>Confirms one coherent ledger and all standard actor-role contexts are registered.</summary>
    [Fact]
    public void Startup_registers_one_emulator_ledger_two_framework_ports_and_all_broker_actor_roles()
    {
        using var container = new Container();
        container.Options.EnableAutoVerification = false;
        container.RegisterInstance<Microsoft.Extensions.Logging.ILogger>(NullLogger.Instance);
        typeof(global::TomasAI.IFM.Application.Actor.IntegrationTests.Startup)
            .GetMethod("RegisterGenericTypes", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [container, new ConfigurationManager(), NullLogger.Instance]);

        var registrations = container.GetCurrentRegistrations();
        ExactlyOne<EmulatorLedger>(registrations);
        ExactlyOne<IFrameworkOrderExecutionBroker>(registrations);
        ExactlyOne<IFrameworkBrokerAccount>(registrations);
        ExactlyOne<ITradeBroker>(registrations);
        ExactlyOne<ICommandActorContext<BrokerOrderCommandActor>>(registrations);
        ExactlyOne<IEventActorContext<BrokerOrderEventActor>>(registrations);
        ExactlyOne<IQueryActorContext<BrokerOrderQueryActor>>(registrations);
        ExactlyOne<IRealtimeActorContext<BrokerOrderRealtimeActor>>(registrations);
        ExactlyOne<ICommandActorContext<BrokerAccountCommandActor>>(registrations);
        ExactlyOne<IEventActorContext<BrokerAccountEventActor>>(registrations);
        ExactlyOne<IQueryActorContext<BrokerAccountQueryActor>>(registrations);

        var orderPort = container.GetInstance<IFrameworkOrderExecutionBroker>();
        var accountPort = container.GetInstance<IFrameworkBrokerAccount>();
        var broker = container.GetInstance<ITradeBroker>();
        orderPort.AccountAlias.Should().Be(accountPort.AccountAlias).And.Be(broker.AccountAlias);
        orderPort.Generation.Should().Be(accountPort.Generation).And.Be(broker.Generation);
        broker.Environment.Should().Be(BrokerEnvironment.Emulator);
    }

    /// <summary>Prevents account repository discovery from creating a conflicting transient registration.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Account_repository_and_reader_share_singleton_registration(bool productionHost)
    {
        using var container = new Container();
        container.Options.EnableAutoVerification = false;
        container.RegisterInstance<Microsoft.Extensions.Logging.ILogger>(NullLogger.Instance);
        var startup = productionHost
            ? typeof(global::TomasAI.IFM.Application.Api.Server.Startup)
            : typeof(global::TomasAI.IFM.Application.Actor.IntegrationTests.Startup);
        startup.GetMethod("RegisterGenericTypes", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [container, new ConfigurationManager(), NullLogger.Instance]);

        var registrations = container.GetCurrentRegistrations();
        var reader = registrations.Single(registration => registration.ServiceType ==
            typeof(TomasAI.IFM.Domain.BrokerAccount.Query.Model.BrokerAccountReadStore));
        var repository = registrations.Single(registration => registration.ServiceType ==
            typeof(TomasAI.IFM.Framework.Storage.IObjectRepository<TomasAI.IFM.Domain.BrokerAccount.Query.Model.BrokerAccountReadStore>));
        repository.Registration.Should().BeSameAs(reader.Registration);
        repository.Lifestyle.Should().Be(Lifestyle.Singleton);
    }

    private static void ExactlyOne<T>(IEnumerable<InstanceProducer> registrations) =>
        registrations.Count(registration => registration.ServiceType == typeof(T)).Should().Be(1);
}

/// <summary>Starts the isolated actor host and verifies the emulator account runtime without a strategy workflow.</summary>
public sealed class TradeBrokerEmulatorHostTests(TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint> sourceFactory)
    : IClassFixture<TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>>
{
    [Fact]
    public async Task Hosted_emulator_places_updates_and_cancels_through_Nats_actor_lifecycle()
    {
        await using var host = sourceFactory.WithWebHostBuilder(builder => builder
            .UseSetting("IFM_TEST_ACTOR_DOMAIN", "TomasAI.IFM.Domain.Trade,TomasAI.IFM.Domain.BrokerAccount,TomasAI.IFM.Domain.Portfolio")
            .UseSetting("IFM_TEST_NATS_URL", DomainActorIntegrationInfrastructureFixture.NatsUrl));
        using var client = host.CreateClient();
        var lifecycle = host.Services.GetRequiredService<ISupervisorManagedActorLifecycle>();
        TomasAI.IFM.Shared.EventModelActor.Contracts.IActorProducer? producer = null;
        IActorEventListener? notifications = null;
        try
        {
            var container = host.Services.GetRequiredService<Container>();
            var broker = container.GetInstance<ITradeBroker>();
            broker.Environment.Should().Be(BrokerEnvironment.Emulator);
            var accounts = container.GetInstance<TomasAI.IFM.Domain.BrokerAccount.Query.Model.IBrokerAccountReadStore>();
            var approval = Guid.NewGuid();
            producer = host.Services.GetRequiredService<TomasAI.IFM.Shared.EventModelActor.Contracts.IActorProducer>();
            await producer.StartAsync(new(TomasAI.IFM.Shared.EventModelActor.ActorType.Realtime, "BrokerUiWorkflowVerification"));
            var accountId = new TomasAI.IFM.Domain.BrokerAccount.Contracts.BrokerAccountId(broker.AccountAlias);
            await WaitAsync(() => Task.FromResult(accounts.Get(accountId)?.Snapshot is { Complete: true } ? accounts.Get(accountId) : null));
            var accountCommands = new TomasAI.IFM.Application.Api.Nats.Client.BrokerAccountCommandApi(producer);
            var manifest = Guid.NewGuid().ToString("N");
            var evidence = await accountCommands.SubmitQualificationEvidenceAsync(accountId, manifest, "isolated-test-evidence", DateTime.UtcNow);
            evidence.Success.Should().BeTrue(evidence.ErrorMessage);
            var accepted = await accountCommands.AcceptQualificationAsync(accountId, approval, manifest, "isolated-test-reviewer", DateTime.UtcNow);
            accepted.Success.Should().BeTrue(accepted.ErrorMessage);
            await WaitAsync(() => Task.FromResult(accounts.Get(accountId)?.Gate == TomasAI.IFM.Domain.BrokerAccount.Contracts.BrokerAccountOperationalGate.Open ? accounts.Get(accountId) : null));
            var componentId = Guid.NewGuid();
            var order = new TomasAI.IFM.Domain.Trade.Shared.TradeOrderDefinition
            {
                Id = new(1, 1, Random.Shared.Next(10000, int.MaxValue)), Revision = 1,
                Status = TomasAI.IFM.Domain.Trade.Shared.TradeOrderStatus.Approved,
                PositionType = TomasAI.IFM.Domain.Trade.Shared.TradeOrderPositionType.Opening,
                ValueDate = DateOnly.FromDateTime(DateTime.UtcNow), ValidUntilUtc = DateTime.UtcNow.AddMinutes(5),
                BrokerEnvironment = TomasAI.IFM.Domain.Trade.Shared.BrokerEnvironment.Emulator,
                BrokerAccountAlias = broker.AccountAlias, PortfolioApprovalId = Guid.NewGuid(),
                AccountPromotionApprovalReference = approval.ToString("N"), DefinitionHash = "hosted-test", MicroExecutionProfileHash = "hosted-profile",
                RequiredCapital = 1000m, MaximumLoss = 1000m, TimeInForce = "GTC", AlgorithmPace = "Patient",
                Components = [new()
                {
                    ComponentId = componentId, ReservedTradeId = 1,
                    StrategyKind = TomasAI.IFM.Domain.Trade.Shared.TradeStrategyKind.FuturesOutright,
                    SignedNetDebitLimit = 100m, MinimumSignedNetDebitLimit = 99m, MaximumSignedNetDebitLimit = 100m, TickIncrement = .25m,
                    Legs = [new() { TradeLegId = Guid.NewGuid(), ContractId = "ES20261218", ContractKey = "ES20261218", SignedQuantity = 1, CashMultiplier = 50m, AssetFamily = TomasAI.IFM.Domain.Trade.Shared.TradeAssetFamily.Futures }]
                }]
            };
            var receivedExecutionEvents = new System.Collections.Concurrent.ConcurrentQueue<TomasAI.IFM.Domain.Trade.Shared.Order.Execution.OrderExecutionChangedEvent>();
            var cancelledNotification = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            notifications = new TomasAI.IFM.Framework.Messaging.NatsJetStream.NatsActorEventListener(
                host.Services.GetRequiredService<TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts.INatsEventListenerOptions>(),
                NullLogger.Instance,
                host.Services.GetRequiredService<TomasAI.IFM.Framework.Messaging.NatsJetStream.NatsConnectionManager>());
            await notifications.StartAsync($"OrderFillsTest-{Guid.NewGuid():N}", new()
            {
                [new(TomasAI.IFM.Shared.EventModelActor.ActorType.Event, "OrderExecutionEvent")] = ["OrderExecutionChanged"]
            }, (_, message) =>
            {
                var value = TomasAI.IFM.Shared.EventModelActor.ActorExtensions.AsEvent<TomasAI.IFM.Domain.Trade.Shared.Order.Execution.OrderExecutionChangedEvent>(message)!;
                if (value.OrderExecutionDefinition.TradeOrderId == order.Id)
                {
                    receivedExecutionEvents.Enqueue(value);
                    if (value.OrderExecutionDefinition.Status == TomasAI.IFM.Domain.Trade.Shared.OrderExecutionStatus.Cancelled)
                        cancelledNotification.TrySetResult();
                }
                return ValueTask.CompletedTask;
            });
            var submitted = await new TomasAI.IFM.Application.Api.Nats.Client.TradeOrderLifecycleApi(producer)
                .SubmitAcceptedAsync(order, order.PortfolioApprovalId, TomasAI.IFM.Domain.Trade.Shared.ExecutionChannel.Broker);
            submitted.Success.Should().BeTrue(submitted.ErrorMessage);
            var query = new TomasAI.IFM.Application.Api.Nats.Client.BrokerOrderQueryApi(producer);
            var commands = new TomasAI.IFM.Application.Api.Nats.Client.BrokerOrderCommandApi(producer);
            var working = await WaitAsync(async () =>
            {
                var result = await query.ListAsync(order.Id);
                return result.Value?.SingleOrDefault(x => x.Status == TomasAI.IFM.Domain.Trade.Shared.Order.Broker.BrokerOrderStatus.Working);
            });
            var updated = await commands.UpdatePriceAsync(working.Id, 99.75m, Guid.NewGuid());
            updated.Success.Should().BeTrue(updated.ErrorMessage);
            await WaitAsync(async () =>
            {
                var result = await query.GetAsync(working.Id);
                return result.Value is { BrokerRevision: 2, CurrentSignedNetDebitLimit: 99.75m, Status: TomasAI.IFM.Domain.Trade.Shared.Order.Broker.BrokerOrderStatus.Working } ? result.Value : null;
            });
            var cancelled = await commands.CancelAsync(working.Id, Guid.NewGuid());
            cancelled.Success.Should().BeTrue(cancelled.ErrorMessage);
            await WaitAsync(async () =>
            {
                var result = await query.GetAsync(working.Id);
                return result.Value?.Status == TomasAI.IFM.Domain.Trade.Shared.Order.Broker.BrokerOrderStatus.Cancelled ? result.Value : null;
            });
            await WaitAsync(async () =>
            {
                var result = await new TomasAI.IFM.Application.Api.Nats.Client.OrderExecutionQueryApi(producer)
                    .GetAsync(order.Id, working.Id.Execution.ExecutionAttemptId);
                return result.Value?.Status == TomasAI.IFM.Domain.Trade.Shared.OrderExecutionStatus.Cancelled ? result.Value : null;
            });
            await cancelledNotification.Task.WaitAsync(TimeSpan.FromSeconds(15));
            receivedExecutionEvents.Should().Contain(x => x.OrderExecutionDefinition.Status == TomasAI.IFM.Domain.Trade.Shared.OrderExecutionStatus.Submitted);
            receivedExecutionEvents.Should().Contain(x => x.OrderExecutionDefinition.Status == TomasAI.IFM.Domain.Trade.Shared.OrderExecutionStatus.Cancelled);
        }
        finally
        {
            if (notifications is not null) await notifications.StopAsync();
            if (producer is not null) await producer.StopAsync();
            (await lifecycle.ShutdownActorsAsync(CancellationToken.None)).Succeeded.Should().BeTrue();
        }
        static async Task<T> WaitAsync<T>(Func<Task<T?>> read) where T : class
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while (DateTime.UtcNow < deadline)
            {
                if (await read() is { } value) return value;
                await Task.Delay(100);
            }
            throw new TimeoutException("Hosted broker actor workflow did not reach the expected state.");
        }
    }

    /// <summary>Starts and cleanly stops the API actor host with one coherent synthetic account.</summary>
    [Fact]
    public async Task Isolated_host_starts_broker_and_account_actors_with_one_coherent_snapshot()
    {
        await using var host = sourceFactory.WithWebHostBuilder(builder => builder
            .UseSetting("IFM_TEST_ACTOR_DOMAIN",
                "TomasAI.IFM.Domain.Trade,TomasAI.IFM.Domain.BrokerAccount")
            .UseSetting("IFM_TEST_NATS_URL", DomainActorIntegrationInfrastructureFixture.NatsUrl));
        using var client = host.CreateClient();
        var lifecycle = host.Services.GetRequiredService<ISupervisorManagedActorLifecycle>();
        try
        {
            var container = host.Services.GetRequiredService<Container>();
            var broker = container.GetInstance<ITradeBroker>();
            var snapshot = await broker.GetAccountSnapshotAsync();
            snapshot.AccountAlias.Should().Be(broker.AccountAlias);
            snapshot.Generation.Should().Be(broker.Generation);
            snapshot.Complete.Should().BeTrue();
            snapshot.Currency.Should().Be("USD");
            container.GetInstance<ICommandActorContext<BrokerOrderCommandActor>>().Should().NotBeNull();
            container.GetInstance<ICommandActorContext<BrokerAccountCommandActor>>().Should().NotBeNull();
        }
        finally
        {
            var shutdown = await lifecycle.ShutdownActorsAsync(CancellationToken.None);
            shutdown.Succeeded.Should().BeTrue(shutdown.FailureReason);
        }
    }
}
