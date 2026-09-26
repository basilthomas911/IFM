using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.EconomicCalendar.Command;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.Architecture;

/// <summary>Guards the canonical contiguous import-command schemas.</summary>
public sealed class ImportCommandWireMigrationTests
{
    [Fact]
    public void Economic_calendar_import_has_contiguous_keys_and_round_trips()
    {
        Keys(typeof(ImportEconomicCalendarsCommand)).Should().Equal(Enumerable.Range(0, 9));
        ImportEconomicCalendarsCommand.Verb.Should().Be("Import");
        var subject = new ActorSubject(ActorType.Command, ImportEconomicCalendarsCommand.Actor,
            ImportEconomicCalendarsCommand.Verb, "calendar");
        var command = new ImportEconomicCalendarsCommand(new DateTime(2026, 9, 24), ["US"])
        {
            CommandId = Guid.NewGuid(),
            Subject = subject,
            PostEvents = true,
        };
        var copy = MessagePackSerializer.Deserialize<ImportEconomicCalendarsCommand>(MessagePackSerializer.Serialize(command));
        copy.CommandId.Should().Be(command.CommandId);
        copy.Subject.Should().Be(subject);
        copy.ImportedDate.Should().Be(command.ImportedDate);
        copy.CountryCodes.Should().Equal("US");
        copy.DuplicatePolicy.Should().Be(command.DuplicatePolicy);
    }

    [Fact]
    public void Yield_curve_import_has_contiguous_keys_and_round_trips()
    {
        Keys(typeof(ImportYieldCurveRatesCommand)).Should().Equal(Enumerable.Range(0, 8));
        ImportYieldCurveRatesCommand.Verb.Should().Be("Import");
        var subject = new ActorSubject(ActorType.Command, ImportYieldCurveRatesCommand.Actor,
            ImportYieldCurveRatesCommand.Verb, "yield");
        var command = new ImportYieldCurveRatesCommand(new DateTime(2026, 9, 24))
        {
            CommandId = Guid.NewGuid(),
            Subject = subject,
            PostEvents = true,
        };
        var copy = MessagePackSerializer.Deserialize<ImportYieldCurveRatesCommand>(MessagePackSerializer.Serialize(command));
        copy.CommandId.Should().Be(command.CommandId);
        copy.Subject.Should().Be(subject);
        copy.ImportDate.Should().Be(command.ImportDate);
        copy.DuplicatePolicy.Should().Be(command.DuplicatePolicy);
    }

    static int[] Keys(Type type) => type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
        .Select(property => property.GetCustomAttribute<KeyAttribute>()?.IntKey)
        .Where(key => key.HasValue)
        .Select(key => key!.Value)
        .Order()
        .ToArray();

}
