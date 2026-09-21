using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

public sealed class EventLogQualificationCatalogTests
{
    [Fact]
    [Trait("Category", "PortfolioLiveHostReference")]
    public async Task Isolated_host_returns_all_current_starter_definitions_as_unpublished_drafts()
    {
        var url = Environment.GetEnvironmentVariable("IFM_NATS_URL");
        url.Should().Be("nats://127.0.0.1:24223", "this qualification targets only the disposable host");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var producer = new NatsActorProducer(new NatsProducerOptions { Url = url! }, Substitute.For<ILogger<NatsActorProducer>>());
        await producer.StartAsync(new ActorMailboxId(ActorType.Query, $"QualificationCatalog{Guid.NewGuid():N}"), timeout.Token);
        try
        {
            var api = new ReferenceQueryApi(producer);
            var defaults = StrategyCatalogDefaults.Create();
            defaults.Should().HaveCount(22);
            foreach (var kind in Enum.GetValues<StrategyCatalogKind>())
            {
                var reply = await api.QueryStrategyCatalogAsync(new(CatalogQueryOperation.List, kind), timeout.Token);
                reply.Success.Should().BeTrue(reply.ErrorMessage);
                var rows = StrategyCatalogJson.Read<StrategyCatalogSummary[]>(reply.Value!);
                // Other explicitly published qualification definitions may coexist.
                // Still require every starter identity and keep its lifecycle untouched.
                var expectedKeys = defaults.Where(x => x.Key.Kind == kind).Select(x => x.Key).ToHashSet();
                var starters = rows.Where(x => expectedKeys.Contains(x.Key)).ToArray();
                starters.Select(x => x.Key).Should().BeEquivalentTo(expectedKeys);
                starters.Should().OnlyContain(x => x.Status == CatalogLifecycleStatus.Draft);
            }
            foreach (var expected in defaults)
            {
                var reply = await api.QueryStrategyCatalogAsync(new(CatalogQueryOperation.Exact, Key: expected.Key), timeout.Token);
                reply.Success.Should().BeTrue(reply.ErrorMessage);
                var actual = StrategyCatalogJson.Read<StoredStrategyCatalogDefinition>(reply.Value!);
                actual.Definition.Key.Should().Be(expected.Key);
                actual.ContentHash.Should().Be(StrategyCatalogValidation.ContentHash(expected));
                actual.Status.Should().Be(CatalogLifecycleStatus.Draft);
                actual.PublishedBy.Should().BeNull();
                actual.EffectiveFromUtc.Should().BeNull();
            }
        }
        finally { await producer.StopAsync(CancellationToken.None); }
    }
}
