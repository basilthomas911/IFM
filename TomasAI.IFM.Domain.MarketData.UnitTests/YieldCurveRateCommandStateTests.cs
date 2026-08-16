using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.Exceptions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public class YieldCurveRateCommandStateTests
{
    [Fact]
    public void ImportReplayTracksExistenceWithoutRetainingRateModels()
    {
        var valueDate = new DateOnly(2026, 8, 5);
        var rate = new YieldCurveRateReadModel(
            valueDate, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var state = new YieldCurveRateCommandState();
        state.ReplayEvents(new IEvent[]
        {
            new YieldCurveRatesImportedEvent { YieldCurveRates = [rate] }
        });
        var duplicate = Route(new AddYieldCurveRateCommand(rate));

        var act = () => duplicate.Execute(state);

        act.Should().Throw<AddYieldCurveRateException>();
    }

    [Fact]
    public void RemoveReplayClearsExistenceForSubsequentAdd()
    {
        var valueDate = new DateOnly(2026, 8, 5);
        var rate = new YieldCurveRateReadModel(
            valueDate, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var state = new YieldCurveRateCommandState();
        state.ReplayEvents(new IEvent[]
        {
            new YieldCurveRatesImportedEvent { YieldCurveRates = [rate] },
            new YieldCurveRateRemovedEvent { ValueDate = valueDate }
        });

        Route(new AddYieldCurveRateCommand(rate)).Execute(state).Should().BeTrue();
    }

    [Fact]
    public void RejectImportThrowsForExistingDate()
    {
        var valueDate = new DateOnly(2026, 8, 5);
        var rate = new YieldCurveRateReadModel(
            valueDate, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var state = new YieldCurveRateCommandState();
        state.ReplayEvents(new IEvent[]
        {
            new YieldCurveRatesImportedEvent { YieldCurveRates = [rate] }
        });
        var command = Route(new ImportYieldCurveRatesCommand(
            valueDate.ToDateTime(TimeOnly.MinValue),
            [rate],
            ImportDuplicatePolicy.Reject));

        var act = () => command.Execute(state);

        act.Should().Throw<MarketDataImportDuplicateException>();
        state.Events.Should().BeEmpty();
    }

    [Fact]
    public void ExactImportDuplicatesCollapseAndPersistEffectivePolicy()
    {
        var valueDate = new DateOnly(2026, 8, 5);
        var rate = new YieldCurveRateReadModel(
            valueDate, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var state = new YieldCurveRateCommandState();
        var command = Route(new ImportYieldCurveRatesCommand(
            valueDate.ToDateTime(TimeOnly.MinValue),
            [rate, rate],
            ImportDuplicatePolicy.Overwrite));

        command.Execute(state).Should().BeTrue();

        var imported = state.Events.Should().ContainSingle()
            .Which.Should().BeOfType<YieldCurveRatesImportedEvent>().Subject;
        imported.YieldCurveRates.Should().ContainSingle();
        imported.DuplicatePolicy.Should().Be(ImportDuplicatePolicy.Overwrite);
    }

    static AddYieldCurveRateCommand Route(AddYieldCurveRateCommand command)
        => command with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                AddYieldCurveRateCommand.Actor,
                AddYieldCurveRateCommand.Verb,
                command.EntityId.Format())
        };

    static ImportYieldCurveRatesCommand Route(ImportYieldCurveRatesCommand command)
        => command with
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                ImportYieldCurveRatesCommand.Actor,
                ImportYieldCurveRatesCommand.Verb,
                command.EntityId.Format())
        };
}
