using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;

internal static class OptionBaselineInspection
{
    internal static async Task RunAsync(string url, DateOnly date, string tradeId)
    {
        ActorExtensions.DataSerializer ??= new NatsMessagePackDataSerializer();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var producer = new NatsActorProducer(new NatsProducerOptions { Url = url }, NullLogger.Instance);
        await producer.StartAsync(new(ActorType.Query, nameof(OptionBaselineInspection)), timeout.Token);
        var response = await new EstablishedTradeQueryApi(producer).GetRealtimeAsync(TradeEntityId.Parse(tradeId), date, timeout.Token);
        var snapshot = response.Value;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            ObservedUtc = DateTimeOffset.UtcNow, response.Success, response.ErrorMessage,
            snapshot?.Status, snapshot?.PlanStatus, snapshot?.PublishedAtUtc,
            PositionSequence = snapshot?.Position?.PositionSequence,
            PlanSequence = snapshot?.Plan?.Position.PositionSequence,
            PlanCalculatedUtc = snapshot?.Plan?.CalculatedAtUtc,
            PlanComplete = snapshot?.Plan?.IronCondorTradePlanSnapshot?.IsComplete,
            PlanCalculationStatus = snapshot?.Plan?.IronCondorTradePlanSnapshot?.CalculationStatus,
            Unavailable = snapshot?.Plan?.IronCondorTradePlanSnapshot?.UnavailableReasons,
            Legs = snapshot?.Legs.Select(leg => new { leg.Definition.ContractId, leg.QuoteStatus, leg.PricingStatus,
                leg.Observation?.ObservedAtUtc, RiskCalculatedAtUtc = leg.LastValidRisk?.CalculatedAtUtc,
                leg.Observation?.ValidUntilUtc, leg.Observation?.OptionPrice })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
