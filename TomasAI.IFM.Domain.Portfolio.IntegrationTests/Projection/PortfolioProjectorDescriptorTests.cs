using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
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
        var projections = Substitute.For<IPortfolioDbContext>();
        var portfolio = new PortfolioEventProjector(replay, source, blackboard, Substitute.For<ILogger<PortfolioEventProjector>>(), events, projections);
        var fund = new PortfolioFundEventProjector(replay, source, blackboard, Substitute.For<ILogger<PortfolioFundEventProjector>>(), events, projections);

        portfolio.ProjectedEventTypes.Should().BeEquivalentTo([
            typeof(PortfolioCreated), typeof(PortfolioVersionAdded), typeof(PortfolioOperatingStateChanged),
            typeof(FundAddedToPortfolio), typeof(PortfolioRetired), typeof(FundAllocationDelegated), typeof(FundRiskEnvelopeDelegated)]);
        fund.ProjectedEventTypes.Should().BeEquivalentTo([
            typeof(FundMandateCreated), typeof(FundMandateVersionAdded), typeof(FundOperatingStateChanged),
            typeof(FundTradeTemplateAssigned), typeof(FundCompositionReserved), typeof(FundCompositionStateChanged)]);
        portfolio.ProjectionDescriptors.Concat(fund.ProjectionDescriptors).Should().OnlyContain(x => x.UseDurableReplay);
        portfolio.DurableProcessQueueName.Should().NotBe(fund.DurableProcessQueueName);
    }
}
