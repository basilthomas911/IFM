using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Projection;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Projection;

public sealed class PortfolioProjectorDescriptorTests
{
    [Fact]
    [Trait("Gate", "PF-09")]
    [Trait("Category", "Portfolio")]
    public void Durable_projectors_cover_every_authoritative_event_with_natural_key_mutation()
    {
        var replay = Substitute.For<IDurableReplayQueue>();
        var source = Substitute.For<IEventSourceActorDbContext>();
        var blackboard = Substitute.For<IBlackboardService>();
        var events = Substitute.For<IPortfolioEventStore>();
        var projections = Substitute.For<IPortfolioDbWriteContext>();
        var portfolio = new PortfolioEventProjector(replay, source, blackboard, Substitute.For<ILogger<PortfolioEventProjector>>(), events, projections);
        var fund = new PortfolioFundEventProjector(replay, source, blackboard, Substitute.For<ILogger<PortfolioFundEventProjector>>(), events, projections);
        var policy = new PortfolioFinancialPolicyEventProjector(replay, source, blackboard, Substitute.For<ILogger<PortfolioFinancialPolicyEventProjector>>(), events, projections);

        portfolio.ProjectedEventTypes.Should().BeEquivalentTo([
            typeof(PortfolioCreatedEvent), typeof(PortfolioVersionAddedEvent), typeof(PortfolioOperatingStateChangedEvent),
            typeof(FundAddedToPortfolioEvent), typeof(PortfolioRetiredEvent), typeof(FundAllocationDelegatedEvent), typeof(FundRiskEnvelopeDelegatedEvent),
            typeof(DraftPortfolioDeletedEvent)]);
        fund.ProjectedEventTypes.Should().BeEquivalentTo([
            typeof(FundMandateCreatedEvent), typeof(FundMandateVersionAddedEvent), typeof(FundOperatingStateChangedEvent),
            typeof(FundTradeTemplateAssignedEvent), typeof(FundCompositionReservedEvent), typeof(FundCompositionStateChangedEvent),
            typeof(FundManualOrderChangedEvent), typeof(FundManualOrderDeletedEvent)]);
        policy.ProjectedEventTypes.Should().BeEquivalentTo([
            typeof(PortfolioFinancialPolicyCreatedEvent), typeof(PortfolioFinancialPolicyVersionAddedEvent),
            typeof(PortfolioFinancialPolicyActivatedEvent), typeof(PortfolioFinancialPolicyRetiredEvent),
            typeof(DraftPortfolioFinancialPolicyDeletedEvent)]);
        portfolio.ProjectionDescriptors.Concat(fund.ProjectionDescriptors).Concat(policy.ProjectionDescriptors).Should().OnlyContain(x => x.UseDurableReplay);
        portfolio.ProjectedEventTypes.Should().BeEquivalentTo(portfolio.ProjectionDescriptors.Select(x => x.SourceEventType));
        fund.ProjectedEventTypes.Should().BeEquivalentTo(fund.ProjectionDescriptors.Select(x => x.SourceEventType));
        policy.ProjectedEventTypes.Should().BeEquivalentTo(policy.ProjectionDescriptors.Select(x => x.SourceEventType));
        new[] { portfolio.DurableProcessQueueName, fund.DurableProcessQueueName, policy.DurableProcessQueueName }.Should().OnlyHaveUniqueItems();
    }
}
