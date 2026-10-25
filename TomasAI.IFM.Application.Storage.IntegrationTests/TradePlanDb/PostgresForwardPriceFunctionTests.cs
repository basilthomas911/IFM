using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Command.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.TestSupport;
using Xunit;
using SessionId = TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanId;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;

/// <summary>Actual Function actors and repositories against disposable PostgreSQL; direct dispatch, not production NATS routing.</summary>
[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresForwardPriceFunctionTests(PostgresTradePlanFixture fixture)
{
    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    public async Task Forward_function_commits_before_reply_and_plan_persists_signed_risk_or_zero_with_error(bool lossScenario, bool fail)
    {
        var databases = Substitute.For<IDbContextFactory>();
        var blackboard = Substitute.For<IBlackboardService>();
        blackboard.EventSourcing.EventStreamId.Returns(new EventStreamIdCacheModel(
            Substitute.For<TomasAI.IFM.Framework.Caching.IRedisCache>(),new TomasAI.IFM.Framework.Serialization.SystemTextJsonSerializer()));
        await using var source = new EventSourceActorDbContext(fixture.Settings,databases,blackboard,NullLogger<DbProvider>.Instance);
        databases.ActorEventSourceDb.Returns(source); databases.TradePlanDb.Returns(fixture.Plans);
        var forwardRepository = new IronCondorForwardPriceFunctionStateRepository(Substitute.For<IEventSourceActorStateFactory>(),source,
            Substitute.For<IActorService>(),NullLogger<IronCondorForwardPriceFunctionStateRepository>.Instance);
        var context = Substitute.For<IIronCondorForwardPriceFunctionContext>();
        context.ActorId.Returns(new ActorMailboxId(ActorType.Function,CalculateIronCondorForwardPriceCommand.Actor));
        context.TimeProvider.Returns(TimeProvider.System); context.Logger.Returns(NullLogger<IronCondorForwardPriceFunctionActor>.Instance);
        context.StateRepository.Returns(forwardRepository);
        var actor = new IronCondorForwardPriceFunctionActor(context);
        var request = IronCondorForwardPriceTestScenario.Request();
        if (lossScenario) request = request with { Position = request.Position with { Legs = request.Position.Legs.Select(leg => leg with {
            OpeningPrice = leg.SignedQuantity < 0 ? 10m : 5m }).ToArray() } };
        if (fail) {
            var legs = request.IronCondorForwardPriceInputs.OptionLegs.ToArray(); legs[0] = legs[0] with { Delta = double.NaN };
            request = request with { IronCondorForwardPriceInputs = request.IronCondorForwardPriceInputs with { OptionLegs = legs } };
        }
        long countAtReply = -1;
        var forwardMessage = new Message<CalculateIronCondorForwardPriceCommand,IronCondorForwardPriceCalculatedEvent>(request) {
            BeforeReply = async () => { countAtReply = await fixture.EventCountAsync(request.StreamId); } };
        await actor.HandleMessageAsync(forwardMessage);
        var forward = forwardMessage.Reply?.Value;
        Assert.True(forward is not null,forwardMessage.Reply?.ErrorMessage ?? "No typed reply");
        Assert.True(fail ? forward!.IsFailed : forward!.IsCompleted,forward!.Failed?.ErrorData + "; " + forward.Failed?.ErrorMessage);
        Assert.Equal(fail ? 0L : 1L,countAtReply);
        if (!fail) {
            Assert.Equal(request.CommandId,forward.Completed!.CommandId);
            Assert.Equal(30m,forward.Completed.IronCondorForwardPrice.ForwardPrice);
            var replay = new Message<CalculateIronCondorForwardPriceCommand,IronCondorForwardPriceCalculatedEvent>(request);
            await actor.HandleMessageAsync(replay);
            Assert.Equal(forward.Completed.Id,replay.Reply!.Value!.Completed!.Id);
            Assert.Equal(1L,await fixture.EventCountAsync(request.StreamId));
        }
        var error = forward.Failed is { } failed ? $"{failed.ErrorData}; {failed.ErrorMessage}" : null;
        var planRequest = IronCondorForwardPriceTestScenario.PlanRequest(request,forward.Completed,error);
        var planRepository = new IronCondorTradePlanFunctionStateRepository(Substitute.For<IEventSourceActorStateFactory>(),source,
            Substitute.For<IActorService>(),Substitute.For<IEventProjector<FuturesIronCondorTradePositionCommandActor>>(),
            NullLogger<IronCondorTradePlanFunctionStateRepository>.Instance,databases,fixture.Transactions);
        var planContext = Substitute.For<IIronCondorTradePlanFunctionContext>();
        planContext.ActorId.Returns(new ActorMailboxId(ActorType.Function,UpdateIronCondorTradePlanCommand.Actor));
        planContext.TimeProvider.Returns(TimeProvider.System); planContext.Logger.Returns(NullLogger<IronCondorTradePlanFunctionActor>.Instance);
        planContext.StateRepository.Returns(planRepository); planContext.Algorithm.Returns(new IronCondorTradePlanAlgorithm());
        var planMessage = new Message<UpdateIronCondorTradePlanCommand,IronCondorTradePlanUpdatedEvent>(planRequest);
        await new IronCondorTradePlanFunctionActor(planContext).HandleMessageAsync(planMessage);
        Assert.True(planMessage.Reply?.Value?.Completed is not null,planMessage.Reply?.Value?.Failed?.ErrorData + "; " + planMessage.Reply?.Value?.Failed?.ErrorMessage + "; " + planMessage.Reply?.ErrorMessage);
        Assert.Equal(1L,await fixture.EventCountAsync(planRequest.StreamId));
        var stored = await fixture.Plans.GetCurrentAsync(request.Position.Id,request.Position.StrategyKind,request.EntityId.ValueDate);
        Assert.NotNull(stored);
        var detail = stored.IronCondorTradePlanSnapshot!;
        if (fail) {
            Assert.Equal(0m,detail.ForwardPrice);
            Assert.Equal($"Forward price calculation error: {error}",detail.ForwardPriceCalculationError);
            Assert.Equal(detail.ForwardPriceCalculationError,stored.Explanation);
            Assert.Null(detail.IronCondorForwardRisk);
        } else {
            Assert.Equal(lossScenario ? -1000m : 0m,detail.IronCondorForwardRisk!.ForwardLoss);
            Assert.Equal(lossScenario ? 10m : 30m,detail.IronCondorForwardRisk.EntryCreditPoints);
            Assert.Equal(lossScenario ? -1000m : 0m,detail.IronCondorForwardRisk.ForwardPnl);
            Assert.Equal(IronCondorForwardRisk.EntryCreditCalculationVersion,detail.IronCondorForwardRisk.CalculationVersion);
            Assert.Equal(lossScenario,stored.RequiresExit);
            Assert.Null(detail.ForwardPriceCalculationError);
            Assert.Equal(request.IronCondorForwardPriceInputs.OptionLegs,detail.IronCondorForwardRisk.IronCondorForwardPrice.IronCondorForwardPriceInputs.OptionLegs);
        }
    }

    sealed class Message<TRequest,TCompleted>(TRequest request) : IActorMessage where TRequest : class,ICommand where TCompleted : class
    {
        public ServiceResult<FunctionResult<TCompleted,TradePlanFailedEvent<SessionId>>>? Reply { get; private set; }
        public Func<Task>? BeforeReply { get; init; }
        public ActorSubject Subject => request.Subject;
        public ActorSubject ReplySubject { get; set; }
        public T? AsCommand<T>() where T : class,ICommand => request as T;
        public T? AsEvent<T>() where T : class,IEvent => default;
        public T? AsQuery<T,TResult>() where T : class,IQuery<TResult> where TResult : class => default;
        public async ValueTask ReplyAsync<T>(T result) where T : class {
            if (BeforeReply is not null) await BeforeReply();
            Reply = result as ServiceResult<FunctionResult<TCompleted,TradePlanFailedEvent<SessionId>>>;
        }
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }
}
