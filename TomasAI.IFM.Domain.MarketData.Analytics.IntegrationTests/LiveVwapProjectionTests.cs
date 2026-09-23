using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit.Abstractions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.IntegrationTests;

public sealed class LiveVwapProjectionTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Live")]
    public async Task Current_es_vwap_projection_advances_and_reports_exactness()
    {
        if (Environment.GetEnvironmentVariable("IFM_LIVE_VWAP_DIAGNOSTIC") != "true") return;
        var connection = new NatsConnectionManager();
        var producer = new NatsActorProducer(new NatsProducerOptions(), NullLogger.Instance, connection);
        await producer.StartAsync(new ActorMailboxId(ActorType.Query,
            $"IFM.VwapDiagnostic.{Guid.NewGuid():N}"), CancellationToken.None);
        try
        {
            var client = new VwapQueryClient(producer);
            var first = await client.ReadAsync();
            Assert.True(first.Success, first.ErrorMessage);
            Assert.NotNull(first.Value);
            await Task.Delay(TimeSpan.FromSeconds(10));
            var second = await client.ReadAsync();
            Assert.True(second.Success, second.ErrorMessage);
            Assert.NotNull(second.Value);
            output.WriteLine("First VWAP={0}; volume={1}; rejected={2}; ordinal={3}; epoch={4}; valid={5}; reason={6}; asOf={7:O}",
                first.Value!.Vwap, first.Value.CumulativeVolume, first.Value.RejectedTradeCount,
                first.Value.LastTradeOrdinal, first.Value.StreamEpochId, first.Value.IsValid,
                first.Value.InvalidReason, first.Value.AsOfUtc);
            output.WriteLine("Second VWAP={0}; volume={1}; rejected={2}; ordinal={3}; epoch={4}; valid={5}; reason={6}; asOf={7:O}",
                second.Value!.Vwap, second.Value.CumulativeVolume, second.Value.RejectedTradeCount,
                second.Value.LastTradeOrdinal, second.Value.StreamEpochId, second.Value.IsValid,
                second.Value.InvalidReason, second.Value.AsOfUtc);
            Assert.True(second.Value.CumulativeVolume > first.Value.CumulativeVolume,
                "Live ES VWAP projection did not include any additional executed volume.");
            Assert.True(second.Value.IsValid
                || second.Value.InvalidReason != FuturesVwapInvalidReason.None,
                "A non-exact live VWAP must disclose its invalid reason.");
        }
        finally
        {
            await producer.StopAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class VwapQueryClient(IActorProducer producer) : NatsClientApi(producer)
    {
        public Task<ServiceResult<FuturesVwapSignalReadModel?>> ReadAsync()
        {
            var valueDate = new DateOnly(2026, 9, 23);
            var entityId = new FuturesVwapSignalEntityId(
                "ES20261218", valueDate, FuturesVwapConfiguration.Standard.ConfigurationId);
            var query = new GetLatestFuturesVwapSignalQuery
            {
                Subject = new ActorSubject(ActorType.Query, GetLatestFuturesVwapSignalQuery.Actor,
                    GetLatestFuturesVwapSignalQuery.Verb, entityId.Format()),
                EntityId = entityId,
                ContractId = entityId.ContractId,
                ValueDate = valueDate,
                ConfigurationId = entityId.ConfigurationId
            };
            return RequestAsync<GetLatestFuturesVwapSignalQuery, FuturesVwapSignalReadModel?>(
                query.Subject, query).AsTask();
        }
    }
}
