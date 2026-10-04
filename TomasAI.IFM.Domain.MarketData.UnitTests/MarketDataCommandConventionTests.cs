using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command.State;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

public sealed class MarketDataCommandConventionTests
{
    static readonly DateTime Date = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Economic_calendar_lifecycle_rejects_invalid_changes_without_pending_events()
    {
        var calendar = new EconomicCalendarReadModel(Date, "US", "Employment", "1", "2", "3", Date, "Test");
        var state = new EconomicCalendarCommandState();
        var add = new AddEconomicCalendarCommand(calendar) { CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, AddEconomicCalendarCommand.Actor, AddEconomicCalendarCommand.Verb, calendar.Id.Format()) };
        Assert.True(add.Execute(state).Success);
        var count = state.Events.Count;
        Assert.False(add.Execute(state).Success);
        Assert.Equal(count, state.Events.Count);
        var updated = calendar with { Actual = "4" };
        var change = new ChangeEconomicCalendarCommand(calendar.Id, updated) { CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, ChangeEconomicCalendarCommand.Actor, ChangeEconomicCalendarCommand.Verb, calendar.Id.Format()) };
        Assert.False((change with { EconomicCalendar = updated with { EventName = "Different event" } }).Execute(state).Success);
        Assert.Equal(count, state.Events.Count);
        Assert.True(change.Execute(state).Success);
        Assert.Equal(updated, Assert.IsType<EconomicCalendarChangedEvent>(state.Events.Last()).EconomicCalendar);
        Assert.Equal("1", calendar.Actual);
        var remove = new RemoveEconomicCalendarCommand(calendar.Id) { CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, RemoveEconomicCalendarCommand.Actor, RemoveEconomicCalendarCommand.Verb, calendar.Id.Format()) };
        Assert.True(remove.Execute(state).Success);
        Assert.False(state.EconomicCalendarExists(calendar.Id));
        count = state.Events.Count;
        Assert.False(remove.Execute(state).Success);
        Assert.False(change.Execute(state).Success);
        Assert.Equal(count, state.Events.Count);
    }

    [Fact]
    public void Yield_curve_lifecycle_preserves_overwrite_and_guards_owning_year()
    {
        var rate = new YieldCurveRateReadModel(DateOnly.FromDateTime(Date), 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var state = new YieldCurveRateCommandState();
        var add = new AddYieldCurveRateCommand(rate) { CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, AddYieldCurveRateCommand.Actor, AddYieldCurveRateCommand.Verb, rate.EntityId.Format()) };
        Assert.True(add.Execute(state).Success);
        var count = state.Events.Count;
        Assert.False(add.Execute(state).Success);
        Assert.Equal(count, state.Events.Count);
        Assert.True((add with { Overwrite = true }).Execute(state).Success);
        var change = new ChangeYieldCurveRateCommand(rate with { OneMonth = 2 }) { CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, ChangeYieldCurveRateCommand.Actor, ChangeYieldCurveRateCommand.Verb, rate.EntityId.Format()) };
        count = state.Events.Count;
        Assert.False((change with { EntityId = new(2025) }).Execute(state).Success);
        Assert.Equal(count, state.Events.Count);
        Assert.True(change.Execute(state).Success);
        Assert.Equal(2, Assert.IsType<YieldCurveRateChangedEvent>(state.Events.Last()).YieldCurveRate.OneMonth);
        Assert.Equal(1, rate.OneMonth);
        var remove = new RemoveYieldCurveRateCommand(rate.ValueDate) { CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, RemoveYieldCurveRateCommand.Actor, RemoveYieldCurveRateCommand.Verb, rate.EntityId.Format()) };
        Assert.True(remove.Execute(state).Success);
        count = state.Events.Count;
        Assert.False(remove.Execute(state).Success);
        Assert.False(change.Execute(state).Success);
        Assert.Equal(count, state.Events.Count);
        Assert.True((change with { Overwrite = true }).Execute(state).Success);
    }

    [Fact]
    public void Import_computation_copies_filters_and_rejects_invalid_request_without_an_event()
    {
        var filters = new[] { "US", "CA" };
        var state = new EconomicCalendarCommandState();
        var command = new ImportEconomicCalendarsCommand(Date, filters) { CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, ImportEconomicCalendarsCommand.Actor, ImportEconomicCalendarsCommand.Verb,
                new EconomicCalendarId(Date, "ZZ", "ImportEconomicCalendars").Format()) };
        Assert.True(command.Execute(state).Success);
        filters[0] = "GB";
        Assert.Equal("US", Assert.IsType<EconomicCalendarsImportedEvent>(Assert.Single(state.Events)).CountryCodes[0]);
        var count = state.Events.Count;
        Assert.False((command with { CountryCodes = ["C1"] }).Execute(state).Success);
        Assert.Equal(count, state.Events.Count);
        var yieldState = new YieldCurveRateCommandState();
        var yieldImport = new ImportYieldCurveRatesCommand(Date) { CommandId = Guid.NewGuid(), EntityId = new(2025) };
        Assert.False(yieldImport.Execute(yieldState).Success);
        Assert.Empty(yieldState.Events);
    }
}
