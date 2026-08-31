using FluentAssertions;
using Newtonsoft.Json;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Persistence;

public sealed class EventHistoryScenarios
{
    [Fact]
    [Trait("Gate", "PF-07")]
    public void Given_serialized_event_history_when_reloaded_then_business_state_is_identical()
    {
        var now = new DateTime(2026, 8, 29, 16, 30, 0, DateTimeKind.Utc);
        var source = new PortfolioAggregate();
        var created = source.Create(Guid.NewGuid(), new PortfolioReadModel
        {
            PortfolioId = 101, Name = "Core", PortfolioVersion = 1,
            OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now,
            CreatedOnUtc = now, CreatedBy = "bdd"
        }, now, "bdd");
        var disabled = source.ChangeState(Guid.NewGuid(), 1, PortfolioOperatingState.Disabled, "pause", now.AddMinutes(1), "bdd");
        var rows = new[] { created, disabled }.Select((item, index) => new EventStreamReadModel
        {
            EventVersion = index + 1,
            StreamVersion = index + 1,
            EventTypeName = item.GetType().AssemblyQualifiedName!,
            EventData = JsonConvert.SerializeObject(item)
        });
        var reloaded = new PortfolioAggregate();

        reloaded.Replay(rows.Select(x => x.ToDomainEvent()).OfType<PortfolioDomainEvent>());

        reloaded.Current.Should().BeEquivalentTo(source.Current);
        reloaded.Revision.Should().Be(2);
    }

    [Fact]
    [Trait("Gate", "PF-07")]
    public void Given_an_acceleration_snapshot_then_snapshot_plus_tail_equals_full_history()
    {
        var now = new DateTime(2026, 8, 29, 16, 30, 0, DateTimeKind.Utc);
        var source = new PortfolioAggregate();
        var first = source.Create(Guid.NewGuid(), new PortfolioReadModel
        {
            PortfolioId = 102, Name = "Alternative", PortfolioVersion = 1,
            OperatingState = PortfolioOperatingState.Draft, EffectiveFromUtc = now, CreatedOnUtc = now, CreatedBy = "bdd"
        }, now, "bdd");
        var snapshot = source.CaptureSnapshot();
        var second = source.ChangeState(Guid.NewGuid(), 1, PortfolioOperatingState.Disabled, "pause", now.AddMinutes(1), "bdd");
        var full = new PortfolioAggregate();
        full.Replay([first, second]);
        var accelerated = new PortfolioAggregate();
        accelerated.RestoreSnapshot(snapshot);
        accelerated.Replay([second]);

        accelerated.Current.Should().BeEquivalentTo(full.Current);
        accelerated.Revision.Should().Be(full.Revision);
    }
}
