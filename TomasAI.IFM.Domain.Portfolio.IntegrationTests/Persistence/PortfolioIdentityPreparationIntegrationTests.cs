using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Identity.PortfolioBusinessIdAllocation.Function.Actor;
using TomasAI.IFM.Domain.Portfolio.Identity.PortfolioBusinessIdAllocation.Function.State;
using TomasAI.IFM.Domain.Portfolio.Operations;
using TomasAI.IFM.Domain.Portfolio.Shared.Common;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"), Trait("Category", "Portfolio")]
public sealed class PortfolioIdentityPreparationIntegrationTests(PortfolioEventStoreFixture fixture)
    : IClassFixture<PortfolioEventStoreFixture>
{
    [Fact]
    public async Task Same_operation_replays_the_PostgreSQL_completion_snapshot_without_allocating_again()
    {
        var allocator = Substitute.For<IPortfolioBusinessIdAllocator>();
        allocator.AllocateFundIdAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(927));
        var request = AllocatePortfolioBusinessIdCommand.ForOperation(Guid.NewGuid()) with
        {
            Kind = PortfolioBusinessIdentityKind.Fund,
            Access = PortfolioAccessContext.Administrator("integration")
        };
        var context = Substitute.For<IFunctionActorContext<PortfolioBusinessIdAllocationFunctionActor>>();
        context.ActorId.Returns(request.Subject.ActorId);
        var factory = Substitute.For<IEventSourceActorStateFactory>();
        factory.CreateState<PortfolioBusinessIdAllocationFunctionState>()
            .Returns(_ => new PortfolioBusinessIdAllocationFunctionState());
        var actorService = Substitute.For<IActorService>();
        PortfolioBusinessIdAllocationFunctionStateRepository Repository() =>
            new(factory, fixture.EventSourceDb, actorService, NullLogger<PortfolioBusinessIdAllocationFunctionStateRepository>.Instance);
        PortfolioBusinessIdAllocationFunctionActor Actor(PortfolioBusinessIdAllocationFunctionStateRepository state) =>
            new(context, state, allocator, Substitute.For<IPortfolioOperationalGuard>(), NullLogger<PortfolioBusinessIdAllocationFunctionActor>.Instance);

        using var first = new AllocationMessage(request);
        await Actor(Repository()).HandleMessageAsync(first);
        (first.Reply?.Value?.IsCompleted ?? false).Should().BeTrue(first.Reply?.Value?.Failed?.ErrorMessage ?? first.Reply?.ErrorMessage);
        var persisted = await Repository().LoadStateAsync(request);
        persisted.CompletedEvent!.Value.Value.Should().Be(927);
        persisted.CompletedEvent.Value.OperationId.Should().Be(request.CommandId);
        persisted.Matches(request).Should().BeTrue();

        using var retry = new AllocationMessage(request);
        await Actor(Repository()).HandleMessageAsync(retry);
        retry.Reply!.Value!.Completed!.Id.Should().Be(first.Reply!.Value!.Completed!.Id);
        using var conflict = new AllocationMessage(request with { Kind = PortfolioBusinessIdentityKind.Order });
        await Actor(Repository()).HandleMessageAsync(conflict);
        conflict.Reply!.Value!.Failed!.ErrorType.Should().Be(ErrorType.Concurrency);
        await allocator.Received(1).AllocateFundIdAsync(Arg.Any<CancellationToken>());
        await allocator.DidNotReceive().AllocateOrderIdAsync(Arg.Any<CancellationToken>());
    }

    sealed class AllocationMessage(AllocatePortfolioBusinessIdCommand command) : IActorMessage
    {
        public ServiceResult<FunctionResult<PortfolioBusinessIdAllocationCompletedEvent, PortfolioBusinessIdAllocationFailedEvent>>? Reply { get; private set; }
        public ActorSubject Subject => command.Subject;
        public ActorSubject ReplySubject { get; set; }
        public T? AsCommand<T>() where T : class, ICommand => command as T;
        public T? AsEvent<T>() where T : class, IEvent => null;
        public T? AsQuery<T, TResult>() where T : class, IQuery<TResult> where TResult : class => null;
        public ValueTask ReplyAsync<T>(T result) where T : class
        {
            Reply = result as ServiceResult<FunctionResult<PortfolioBusinessIdAllocationCompletedEvent, PortfolioBusinessIdAllocationFailedEvent>>;
            return ValueTask.CompletedTask;
        }
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }
}
