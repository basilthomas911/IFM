using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Persistence;

public sealed class PortfolioEventStoreContractTests
{
    [Fact]
    [Trait("Gate", "PF-07")]
    public void Stream_names_are_stable_and_parent_scoped()
    {
        PortfolioEventStore.PortfolioStream(new PortfolioId(101)).Should().Be("Portfolio.101");
        PortfolioEventStore.FundStream(new PortfolioFundId(101, 205)).Should().Be("PortfolioFund.101.205");
    }

    [Fact]
    [Trait("Gate", "PF-07")]
    public void Portfolio_events_are_native_event_source_contracts()
    {
        var now = DateTime.SpecifyKind(DateTime.Parse("2026-08-29T16:00:00"), DateTimeKind.Utc);
        IEvent domainEvent = new PortfolioCreated(Guid.NewGuid(), Guid.NewGuid(), 1, now, "unit", new PortfolioReadModel
        {
            PortfolioId = 101, Name = "Core", PortfolioVersion = 1,
            OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "unit"
        });

        domainEvent.EventName.Should().Be(nameof(PortfolioCreated));
        domainEvent.EventType.Should().Be(EventType.DomainEvent);
    }
}
