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

    private static void ExactlyOne<T>(IEnumerable<InstanceProducer> registrations) =>
        registrations.Count(registration => registration.ServiceType == typeof(T)).Should().Be(1);
}

/// <summary>Starts the isolated actor host and verifies the emulator account runtime without a strategy workflow.</summary>
public sealed class TradeBrokerEmulatorHostTests(WebApplicationFactory<Program> sourceFactory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    /// <summary>Starts and cleanly stops the API actor host with one coherent synthetic account.</summary>
    [Fact]
    public async Task Isolated_host_starts_broker_and_account_actors_with_one_coherent_snapshot()
    {
        await using var host = sourceFactory.WithWebHostBuilder(builder => builder
            .UseSetting("IFM_TEST_ACTOR_DOMAIN",
                "TomasAI.IFM.Domain.Trade,TomasAI.IFM.Domain.BrokerAccount")
            .UseSetting("IFM_TEST_NATS_URL", "nats://127.0.0.1:14222"));
        using var client = host.CreateClient();
        var supervisor = host.Services.GetRequiredService<IActorSupervisor>();
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
            await supervisor.ShutdownAsync();
        }
    }
}
