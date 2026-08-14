using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Fund.Transaction.Command.EventProjector;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Fund.UnitTests.Transaction;

public sealed class FundTransactionEventProjectorTests
{
    [Fact]
    public void All_transaction_mutations_are_unique_and_durable()
    {
        var projector = new FundTransactionEventProjector(
            Substitute.For<IDbContextFactory>(), Substitute.For<IDurableReplayQueue>(),
            Substitute.For<IEventSourceActorDbContext>(), Substitute.For<IBlackboardService>(),
            Substitute.For<ILogger<FundTransactionEventProjector>>());

        projector.ProjectionDescriptors.Should().HaveCount(3);
        projector.ProjectionDescriptors.Select(descriptor => descriptor.SourceEventType)
            .Should().OnlyHaveUniqueItems();
        projector.ProjectionDescriptors.Should().OnlyContain(descriptor => descriptor.UseDurableReplay);
    }
}
