using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Actor;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.EventProjector;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.State;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.DownloadLog;

[Trait("Category", "Verification")]
public sealed class DownloadLogVerificationTests
{
    [Fact] public void State_reconstruction_preserves_identity_hash_and_measurements_without_new_events()
    {
        var source = new DownloadLogCommandState(); var command = new InsertMarketDataDownloadLogCommand(DownloadLogContractTests.Outcome());
        command.Execute(source);
        var restored = new DownloadLogCommandState(); restored.ReplayEvents(new DomainEventCollection(source.Events));
        Assert.True(restored.VerifyDuplicate(command)); Assert.Empty(restored.Events);
    }

    [Fact] public void Command_maps_and_query_maps_have_exact_matching_contract_sets()
    {
        static Type[] Types(Type actor, string map) => ((System.Collections.IDictionary)actor.GetField(map, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!).Keys.Cast<Type>().OrderBy(t => t.Name).ToArray();
        Assert.Equal(Types(typeof(DownloadLogCommandActor), "_validationMap"), Types(typeof(DownloadLogCommandActor), "_receiveMap"));
        Assert.Equal(Types(typeof(DownloadLogQueryActor), "_exceptionMap"), Types(typeof(DownloadLogQueryActor), "_receiveMap"));
        Assert.Equal(3, Types(typeof(DownloadLogQueryActor), "_receiveMap").Length);
    }

    [Fact] public async Task Durable_descriptor_replays_only_the_original_log_and_propagates_storage_failure_and_cancellation()
    {
        var db = Substitute.For<IMarketDataDbContext>(); var factory = Substitute.For<IDbContextFactory>(); factory.MarketDataDb.Returns(db);
        var ctx = Context(factory); var projector = new DownloadLogEventProjector(ctx);
        var descriptor = Assert.Single(projector.ProjectionDescriptors);
        Assert.True(descriptor.UseDurableReplay); Assert.Equal(typeof(MarketDataDownloadLogInsertedEvent), descriptor.SourceEventType);
        var requirement = new MarketDataDownloadLogInsertedEvent().RequiredProjection;
        Assert.Equal(projector.ActorName, requirement.ActorName);
        Assert.Equal(projector.ProjectorName, requirement.ProjectorName);
        Assert.Equal(EventProjectorStageType.PublishProcessingEvent, requirement.InitialStage);
        var o = DownloadLogContractTests.Outcome(); var command = new InsertMarketDataDownloadLogCommand(o);
        var inserted = new MarketDataDownloadLogInsertedEvent { Outcome = o, CommandId = command.CommandId, EntityId = command.EntityId, PayloadSha256 = command.PayloadSha256 };
        using var cancellation = new CancellationTokenSource();
        var execution = new ProjectionExecutionContext(projector.ProjectorName, 1, 1,
            new EventProjectorEffectIdentity(projector.ProjectorName, 1, EventProjectorEffectKind.TargetProjection), Guid.NewGuid(), EventProjectionIdempotencyStrategy.NaturalKeyMutation, cancellation.Token);
        await descriptor.ApplyAsync(inserted, execution); await descriptor.ApplyAsync(inserted, execution);
        await db.Received(2).InsertMarketDataDownloadLogAsync(o, command.CommandId, command.PayloadSha256, cancellation.Token);
        db.InsertMarketDataDownloadLogAsync(o, command.CommandId, command.PayloadSha256, cancellation.Token).Returns(Task.FromException(new InvalidOperationException("Scylla unavailable")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => descriptor.ApplyAsync(inserted, execution).AsTask());
        Assert.IsType<MarketDataDownloadLogInsertedCompleteEvent>(descriptor.CompletedEventFactory(inserted));
        Assert.IsType<MarketDataDownloadLogInsertedFailEvent>(descriptor.FailedEventFactory(inserted, new Exception("Projection failed")));
        Assert.Equal(o, inserted.Outcome);
    }

    [Fact] public async Task Actor_starts_and_stops_its_durable_projector()
    {
        var ctx = Context(Substitute.For<IDbContextFactory>()); var projector = Substitute.For<IEventProjector<DownloadLogCommandActor>>();
        var container = Substitute.For<IContainerInstance>(); ctx.Container.Returns(container);
        container.Resolve<IEventSourceActorStateRepository<DownloadLogCommandState>>().Returns(Substitute.For<IEventSourceActorStateRepository<DownloadLogCommandState>>());
        var actor = new DownloadLogCommandActor(ctx, projector);
        await ((ICommandActor<DownloadLogCommandActor>)actor).OnStartup(ctx);
        await projector.Received(1).StartAsync(ctx, CancellationToken.None);
        await ((ICommandActor<DownloadLogCommandActor>)actor).OnShutdown(ctx);
        await projector.Received(1).StopAsync();
    }

    static IDownloadLogCommandContext Context(IDbContextFactory factory)
    {
        var ctx = Substitute.For<IDownloadLogCommandContext>();
        ctx.ActorId.Returns(new ActorMailboxId(ActorType.Command, DownloadLogCommandActor.ActorName));
        ctx.Logger.Returns(NullLogger<DownloadLogCommandActor>.Instance); ctx.DbFactory.Returns(factory);
        ctx.DbEventSource.Returns(Substitute.For<IEventSourceActorDbContext>());
        ctx.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        ctx.BlackboardService.Returns(Substitute.For<IBlackboardService>());
        return ctx;
    }

    [Fact] public async Task Query_errors_are_typed_failures_and_cancellation_does_not_publish_a_result()
    {
        var ctx = Substitute.For<IDownloadLogQueryContext>();
        ctx.Logger.Returns(NullLogger<DownloadLogQueryActor>.Instance);
        ctx.ActorId.Returns(new ActorMailboxId(ActorType.Query, DownloadLogQueryActor.ActorName));
        var actor = new DownloadLogQueryActor(ctx);
        var query = new GetMarketDataDownloadStatusQuery(new(MarketDataDownloadDataset.EconomicCalendar, "FMP", "US", new(2026, 9, 5)));
        ServiceResult<MarketDataDownloadStatusResult>? reply = null;
        ctx.ReplyAsync(Arg.Any<ActorThreadId>(), Arg.Any<string>(), Arg.Do<ServiceResult<MarketDataDownloadStatusResult>>(r => reply = r)).Returns(ValueTask.CompletedTask);
        await ((IQueryActor<DownloadLogQueryActor>)actor).OnExceptionAsync(ctx, query.Subject.ThreadId, query,
            GetMarketDataDownloadStatusQuery.Verb, new InvalidOperationException("Storage unavailable"));
        Assert.NotNull(reply); Assert.False(reply.Success); Assert.Null(reply.Value);
        ctx.ClearReceivedCalls();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => actor.HandleMessageAsync(Substitute.For<IActorMessage>(), query.Subject.ThreadId, cancellation.Token).AsTask());
        Assert.DoesNotContain(ctx.ReceivedCalls(), c => c.GetMethodInfo().Name == "ReplyAsync");
    }
}
