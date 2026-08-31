using FluentAssertions;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using NATS.Net;
using NSubstitute;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.Portfolio.Shared.Commands;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using System.Diagnostics;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

public sealed class PortfolioRealNatsRouteTests
{
    [Fact]
    [Trait("Gate", "PF-10")]
    [Trait("Category", "Portfolio")]
    public async Task Typed_command_client_round_trips_over_real_NATS_with_exact_subject_and_correlation()
    {
        var url = Environment.GetEnvironmentVariable("IFM_NATS_URL") ?? "nats://localhost:4222";
        var portfolioId = Math.Abs(Guid.NewGuid().GetHashCode()) + 1000;
        var actorSubject = new ActorSubject(ActorType.Command, PortfolioCommandSubjects.PortfolioActor, "CreatePortfolio", new PortfolioId(portfolioId).Format());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var responder = new NatsClient(url);
        await responder.ConnectAsync();
        var captured = new TaskCompletionSource<PortfolioCommand<CreatePortfolioPayload, PortfolioId>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var responderTask = Task.Run(async () =>
        {
            await foreach (var message in responder.SubscribeAsync<PortfolioCommand<CreatePortfolioPayload, PortfolioId>>(
                               actorSubject.ToString(),
                               serializer: NatsMessagePackSerializer<PortfolioCommand<CreatePortfolioPayload, PortfolioId>>.Default,
                               cancellationToken: timeout.Token))
            {
                captured.TrySetResult(message.Data!);
                var bytes = new NatsMessagePackDataSerializer().Serialize<ServiceResult<GuidResult>>(
                    new ServiceOk<GuidResult>(new(message.Data!.CommandId)));
                await message.ReplyAsync(bytes, serializer: new NatsByteArrayMessageSerializer(), cancellationToken: timeout.Token);
                break;
            }
        }, timeout.Token);
        await Task.Delay(100, timeout.Token);

        var producer = new NatsActorProducer(new NatsProducerOptions { Url = url }, Substitute.For<ILogger<NatsActorProducer>>());
        await producer.StartAsync(new ActorMailboxId(ActorType.Command, $"PortfolioTestClient{Guid.NewGuid():N}"), timeout.Token);
        try
        {
            using var activity = new Activity("portfolio-real-nats-correlation").SetIdFormat(ActivityIdFormat.W3C).Start();
            var expectedCorrelation = Guid.ParseExact(activity.TraceId.ToHexString(), "N");
            var api = new PortfolioCommandApi(producer);
            var result = await api.CreatePortfolioAsync(
                new PortfolioReadModel { PortfolioId = portfolioId, Name = "NATS route" },
                Guid.NewGuid(),
                timeout.Token);
            var command = await captured.Task.WaitAsync(timeout.Token);
            await responderTask.WaitAsync(timeout.Token);

            result.Success.Should().BeTrue();
            result.Value.Should().Be(command.CommandId);
            command.Subject.Should().Be(actorSubject);
            command.EntityId.Should().Be(new PortfolioId(portfolioId));
            command.Payload.Portfolio.PortfolioId.Should().Be(portfolioId);
            command.CorrelationId.Should().Be(expectedCorrelation);
            command.RequestedOnUtc.Kind.Should().Be(DateTimeKind.Utc);
        }
        finally
        {
            await producer.StopAsync(timeout.Token);
        }
    }
}
