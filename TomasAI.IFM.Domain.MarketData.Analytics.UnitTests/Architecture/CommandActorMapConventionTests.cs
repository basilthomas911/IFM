using System.Collections;
using System.Reflection;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.Architecture;

/// <summary>
/// Verifies that every Market Data Analytics Command actor exposes the three
/// command maps required by the system actor implementation convention.
/// </summary>
public sealed class CommandActorMapConventionTests
{
    /// <summary>
    /// Ensures every concrete event-sourced Command actor has non-empty parse,
    /// validation, and receive maps with matching command counts.
    /// </summary>
    [Fact]
    public void ConcreteCommandActors_ShouldExposeCompleteCommandMaps()
    {
        var commandActors = typeof(FuturesAdxSignalCommandActor).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && InheritsOpenGeneric(
                type,
                typeof(BaseEventSourceCommandActor<>)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        commandActors.Should().NotBeEmpty();

        foreach (var commandActor in commandActors)
        {
            var parseMap = GetMap(commandActor, "_parseMap");
            var validationMap = GetMap(commandActor, "_validationMap");
            var receiveMap = GetMap(commandActor, "_receiveMap");

            parseMap.Count.Should().BeGreaterThan(0,
                $"{commandActor.Name} must declare at least one parsed command");
            validationMap.Count.Should().Be(parseMap.Count,
                $"{commandActor.Name} must validate every parsed command");
            receiveMap.Count.Should().Be(parseMap.Count,
                $"{commandActor.Name} must receive every parsed command");
        }
    }

    static IDictionary GetMap(Type commandActor, string fieldName)
    {
        var field = commandActor.GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        field.Should().NotBeNull($"{commandActor.Name} must declare {fieldName}");
        var map = field!.GetValue(null) as IDictionary;
        map.Should().NotBeNull($"{commandActor.Name}.{fieldName} must be a dictionary");
        return map!;
    }

    static bool InheritsOpenGeneric(Type candidate, Type openGenericBase)
    {
        for (var current = candidate.BaseType; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == openGenericBase)
                return true;
        }

        return false;
    }
}
