using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public class YieldCurveRateCommandStateTests
{
    [Fact]
    public void ImportReplayDoesNotRebuildExternalRecords()
    {
        var valueDate = new DateOnly(2026, 8, 5);
        var rate = new YieldCurveRateReadModel(
            valueDate, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var state = new YieldCurveRateCommandState();
        state.ReplayEvents(new IEvent[]
        {
            new YieldCurveRatesImportedEvent { ImportDate = valueDate.ToDateTime(TimeOnly.MinValue) }
        });
        var duplicate = Route(new AddYieldCurveRateCommand(rate));

        var act = () => duplicate.Execute(state);

        act.Should().NotThrow();
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
            new YieldCurveRateAddedEvent { YieldCurveRate = rate },
            new YieldCurveRateRemovedEvent { ValueDate = valueDate }
        });

        Route(new AddYieldCurveRateCommand(rate)).Execute(state).Should().BeTrue();
    }

    [Fact]
    public void RejectImportRecordsIntentForStorageEnforcement()
    {
        var valueDate = new DateOnly(2026, 8, 5);
        var rate = new YieldCurveRateReadModel(
            valueDate, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var state = new YieldCurveRateCommandState();
        state.ReplayEvents(new IEvent[]
        {
            new YieldCurveRatesImportedEvent { ImportDate = valueDate.ToDateTime(TimeOnly.MinValue) }
        });
        var command = Route(new ImportYieldCurveRatesCommand(
            valueDate.ToDateTime(TimeOnly.MinValue),
            ImportDuplicatePolicy.Reject));

        command.Execute(state).Should().BeTrue();
        state.Events.Should().ContainSingle()
            .Which.Should().BeOfType<YieldCurveRatesImportedEvent>()
            .Which.DuplicatePolicy.Should().Be(ImportDuplicatePolicy.Reject);
    }

    [Fact]
    public void ImportPersistsRequestDateAndEffectivePolicy()
    {
        var valueDate = new DateOnly(2026, 8, 5);
        var rate = new YieldCurveRateReadModel(
            valueDate, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var state = new YieldCurveRateCommandState();
        var command = Route(new ImportYieldCurveRatesCommand(
            valueDate.ToDateTime(TimeOnly.MinValue),
            ImportDuplicatePolicy.Overwrite));

        command.Execute(state).Should().BeTrue();

        var imported = state.Events.Should().ContainSingle()
            .Which.Should().BeOfType<YieldCurveRatesImportedEvent>().Subject;
        imported.ImportDate.Should().Be(valueDate.ToDateTime(TimeOnly.MinValue));
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
