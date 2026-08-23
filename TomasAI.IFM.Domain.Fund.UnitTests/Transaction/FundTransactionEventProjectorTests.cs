using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Transaction.Command.EventProjector;
using TomasAI.IFM.Domain.Fund.Transaction.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.UnitTests.Transaction;

public sealed class FundTransactionEventProjectorTests
{
    [Fact]
    public void All_transaction_mutations_are_unique_and_durable()
    {
        var context = Substitute.For<IFundTransactionCommandContext>();
        context.DbFactory.Returns(Substitute.For<IDbContextFactory>());
        context.DurableReplayQueue.Returns(Substitute.For<IDurableReplayQueue>());
        context.DbEventSource.Returns(Substitute.For<IEventSourceActorDbContext>());
        context.BlackboardService.Returns(Substitute.For<IBlackboardService>());
        context.Logger.Returns(Substitute.For<ILogger<FundTransactionCommandActor>>());
        var projector = new FundTransactionEventProjector(context);

        projector.ProjectionDescriptors.Should().HaveCount(3);
        projector.ProjectionDescriptors.Select(descriptor => descriptor.SourceEventType)
            .Should().OnlyHaveUniqueItems();
        projector.ProjectionDescriptors.Should().OnlyContain(descriptor => descriptor.UseDurableReplay);
    }
}
