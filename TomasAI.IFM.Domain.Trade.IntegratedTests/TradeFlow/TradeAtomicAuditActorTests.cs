using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.TradeFlow;

public sealed partial class TradeBrokerEmulatorHostTests
{
    static void ConfigureAuditHost(IWebHostBuilder builder)
    {
        builder.UseSetting("IFM_TEST_ACTOR_DOMAIN", "TomasAI.IFM.Domain.Trade,TomasAI.IFM.Domain.BrokerAccount,TomasAI.IFM.Domain.Portfolio")
            .UseSetting("IFM_TEST_NATS_URL", DomainActorIntegrationInfrastructureFixture.NatsUrl)
            .ConfigureServices(services =>
            {
                // The feed watchdog is outside this broker/persistence test scope.
                // Its full-host shutdown currently races service-provider disposal.
                var watchdog = services.Single(descriptor => descriptor.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                    && descriptor.ImplementationFactory?.Method.ReturnType == typeof(TomasAI.IFM.Application.MarketData.Databento.Resiliency.DatabentoMarketDataWatchdogService));
                services.Remove(watchdog);
            });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Hosted_trade_creation_commits_once_rejects_conflicts_and_does_not_acknowledge_orphans(bool option)
    {
        await using var host = sourceFactory.WithWebHostBuilder(ConfigureAuditHost);
        using var client = host.CreateClient();
        var lifecycle = host.Services.GetRequiredService<ISupervisorManagedActorLifecycle>();
        var producer = host.Services.GetRequiredService<IActorProducer>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await producer.StartAsync(new(ActorType.Realtime, "AtomicTradeAuditVerification"));
        try
        {
            var container = host.Services.GetRequiredService<Container>();
            var factory = container.GetInstance<IDbContextFactory>();
            factory.TradeDb.ProviderName.Should().Be("System.Data.Postgres");
            var id = new TradeEntityId(1, 1, Random.Shared.Next(10000, int.MaxValue), 1);
            var component = Guid.NewGuid();
            var attempt = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var family = option ? TradeAssetFamily.FuturesOption : TradeAssetFamily.Futures;
            var legs = Enumerable.Range(0, option ? 2 : 1).Select(index => new TradeLegDefinition
            {
                TradeLegId = Guid.NewGuid(), ContractId = option ? $"ES20261218C{6000 + index * 50}" : "ES20261218",
                ContractKey = option ? $"ES20261218C{6000 + index * 50}" : "ES20261218",
                AssetFamily = family, SignedQuantity = index == 0 ? 1 : -1,
                Expiry = new(2026, 12, 18), PutCall = option ? (byte)1 : null, Strike = option ? 6000 + index * 50 : null,
                CashMultiplier = 50m
            }).ToArray();
            var trade = new EstablishedTradeDefinition
            {
                Id = id, AssetFamily = family,
                StrategyKind = option ? TradeStrategyKind.VerticalSpread : TradeStrategyKind.FuturesOutright,
                SourceComponentId = component, ExecutionAttemptId = attempt, Status = EstablishedTradeStatus.Open,
                Legs = legs, EstablishedAtUtc = now, EvidenceRevision = 1,
                OriginalFills = legs.Select(leg => new ExecutionFillEvidence
                {
                    ExecutionFillId = Guid.NewGuid(), ExecutionAttemptId = attempt, ComponentId = component,
                    TradeLegId = leg.TradeLegId, ContractId = leg.ContractId, SignedQuantity = leg.SignedQuantity,
                    Price = 100m, FilledAtUtc = now, ExternalExecutionId = Guid.NewGuid().ToString("N")
                }).ToArray()
            };
            var commandId = Guid.NewGuid();
            var actor = option ? FuturesOptionTradeActorNames.Command : FuturesTradeActorNames.Command;
            var verb = option ? CreateOptionTradeCommand.Verb : CreateFuturesTradeCommand.Verb;
            var subject = new ActorSubject(ActorType.Command, actor, verb, id.Format());
            var futures = new CreateFuturesTradeCommand { CommandId = commandId, Subject = subject, EntityId = id, Trade = trade };
            var options = new CreateOptionTradeCommand { CommandId = commandId, Subject = subject, EntityId = id, Trade = trade };
            async Task<ServiceResult<GuidResult>> Send(bool conflict = false, Guid? replacementId = null)
            {
                var payload = conflict ? trade with { OpeningValue = 123m } : trade;
                return option
                    ? await producer.RequestAsync<CreateOptionTradeCommand, TradeEntityId, GuidResult>(subject,
                        options with { CommandId = replacementId ?? commandId, Trade = payload }, id, deadline.Token)
                    : await producer.RequestAsync<CreateFuturesTradeCommand, TradeEntityId, GuidResult>(subject,
                        futures with { CommandId = replacementId ?? commandId, Trade = payload }, id, deadline.Token);
            }
            var first = await Send();
            first.Success.Should().BeTrue(first.ErrorMessage);
            var retries = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Send()));
            retries.Should().OnlyContain(result => result.Success);
            (await Send(conflict: true)).Success.Should().BeFalse();
            var stored = await factory.TradeDb.GetEstablishedTradeAsync(id, deadline.Token);
            stored.Should().BeEquivalentTo(trade);

            // A separately committed reservation from an older writer must never
            // become a success acknowledgement without its own source event.
            var orphanId = Guid.NewGuid();
            var transactions = container.GetInstance<IPostgresEventTransaction>();
            await transactions.ExecuteOnceAsync(async (db, token) =>
            {
                await db.ReserveCommandAsync(option ? options with { CommandId = orphanId }
                    : futures with { CommandId = orphanId }, token);
                return true;
            }, deadline.Token);
            (await Send(replacementId: orphanId)).Success.Should().BeFalse();
            var count = await transactions.ExecuteOnceAsync((db, token) => db.ScalarAsync(
                "SELECT count(*) FROM event_log WHERE commandid=$1;", [commandId], token), deadline.Token);
            Convert.ToInt64(count).Should().Be(1);
        }
        finally
        {
            await producer.StopAsync();
            (await lifecycle.ShutdownActorsAsync(CancellationToken.None)).Succeeded.Should().BeTrue();
            (await host.Services.GetRequiredService<ISupervisorBootstrap>().StopSupervisorAsync(CancellationToken.None)).Succeeded.Should().BeTrue();
        }
    }
}
