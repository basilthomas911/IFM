using System.Collections;
using System.Reflection;
using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.Actor.IntegrationTests.Architecture;

/// <summary>Enumerates published actor-message schemas before each staged compatibility migration.</summary>
public sealed class ActorMessageContractConventionInventoryTests(ITestOutputHelper output)
{
    static readonly string[] AssemblyNames =
    [
        "TomasAI.IFM.Domain.Application.Shared",
        "TomasAI.IFM.Domain.MarketData.Analytics.Shared",
        "TomasAI.IFM.Domain.MarketData.Feed.Shared",
        "TomasAI.IFM.Domain.MarketData.Shared",
        "TomasAI.IFM.Domain.OptionPricer.Shared",
        "TomasAI.IFM.Domain.Portfolio.Shared",
        "TomasAI.IFM.Domain.PredictiveModel.Shared",
        "TomasAI.IFM.Domain.Reference.Shared",
        "TomasAI.IFM.Domain.Strategy.Contracts.Shared",
        "TomasAI.IFM.Domain.SystemAdmin.Shared",
        "TomasAI.IFM.Domain.Trade.Shared",
    ];

    static readonly string[] ActorAssemblyNames =
    [
        "TomasAI.IFM.Domain.Application.Actor",
        "TomasAI.IFM.Domain.BrokerAccount",
        "TomasAI.IFM.Domain.MarketData.Analytics",
        "TomasAI.IFM.Domain.MarketData.Feed",
        "TomasAI.IFM.Domain.MarketData.Securities",
        "TomasAI.IFM.Domain.MarketData",
        "TomasAI.IFM.Domain.OptionPricer",
        "TomasAI.IFM.Domain.Portfolio",
        "TomasAI.IFM.Domain.Reference",
        "TomasAI.IFM.Domain.SystemAdmin",
        "TomasAI.IFM.Domain.Trade",
    ];

    [Fact]
    public void Inventory_every_shared_actor_message_schema()
    {
        var mapped = new HashSet<Type>();
        foreach (var name in ActorAssemblyNames)
        {
            Assembly assembly;
            try { assembly = Assembly.Load(name); }
            catch (Exception error)
            {
                output.WriteLine(name + ": actor assembly load failed: " + error.GetType().Name);
                continue;
            }
            foreach (var actor in assembly.GetTypes().Where(type => type.IsClass && type.Name.EndsWith("Actor", StringComparison.Ordinal)))
            {
                var field = actor.GetField("_receiveMap", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field?.GetValue(null) is not IEnumerable entries) continue;
                foreach (var entry in entries)
                    if (entry.GetType().GetProperty("Key")?.GetValue(entry) is Type message)
                        mapped.Add(message);
            }
        }
        var messages = new List<(Type Type, string Role)>();
        var findings = new List<string>();
        foreach (var name in AssemblyNames)
        {
            Assembly assembly;
            try { assembly = Assembly.Load(name); }
            catch (Exception error)
            {
                findings.Add(name + ": assembly load failed: " + error.GetType().Name);
                continue;
            }

            foreach (var type in assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false } && !type.ContainsGenericParameters))
            {
                if (!mapped.Contains(type)) continue;
                var role = typeof(ICommand).IsAssignableFrom(type) ? "Command" :
                    typeof(IQuery).IsAssignableFrom(type) ? "Query" :
                    typeof(IEvent).IsAssignableFrom(type) ? "Event" : null;
                if (role is null) continue;
                messages.Add((type, role));
                var attribute = type.GetCustomAttribute<MessagePackObjectAttribute>(inherit: false);
                if (attribute is null)
                {
                    findings.Add(type.FullName + ": missing direct MessagePackObject");
                    continue;
                }
                if (!attribute.AllowPrivate)
                    findings.Add(type.FullName + ": AllowPrivate is false");

                var keys = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>(inherit: false)))
                    .Where(item => item.Key?.IntKey is not null)
                    .Select(item => (Key: item.Key!.IntKey!.Value, Name: item.Property.Name))
                    .OrderBy(item => item.Key)
                    .ToArray();
                if (keys.Length == 0)
                {
                    findings.Add(type.FullName + ": no direct numeric keys");
                    continue;
                }
                if (!keys.Select(item => item.Key).SequenceEqual(Enumerable.Range(0, keys.Length)))
                    findings.Add(type.FullName + ": noncontiguous or duplicate keys");
                var expected = role switch
                {
                    "Command" => new[] { "CommandId", "Subject", "PostEvents", "EntityId", "ErrorCode", "RouteTo" },
                    "Query" => new[] { "Subject", "EntityId" },
                    "Event" when typeof(ICompleteEvent).IsAssignableFrom(type) || typeof(IErrorEvent).IsAssignableFrom(type) =>
                        new[] { "Subject" },
                    _ => new[] { "Subject", "Id", "EntityId", "EventId", "CommandId", "AggregateId", "EventSource", "ReceivedOn" },
                };
                for (var index = 0; index < Math.Min(expected.Length, keys.Length); index++)
                    if (keys[index].Name != expected[index])
                        findings.Add(type.FullName + ": key " + index + " is " + keys[index].Name + ", expected " + expected[index]);
                if (keys.Length < expected.Length)
                    findings.Add(type.FullName + ": missing standard envelope keys");
                if (type.GetConstructor(Type.EmptyTypes) is null)
                    findings.Add(type.FullName + ": missing public parameterless constructor");
                if (!type.GetConstructors().Any(ctor => ctor.GetCustomAttribute<SerializationConstructorAttribute>() is not null))
                    findings.Add(type.FullName + ": missing public SerializationConstructor");
            }
        }
        output.WriteLine("Mapped message types: " + mapped.Count + "; inspected Shared contracts: " + messages.Count);
        foreach (var type in mapped.Where(type => !AssemblyNames.Contains(type.Assembly.GetName().Name)).OrderBy(type => type.FullName))
            output.WriteLine("Mapped outside a domain Shared assembly: " + type.FullName);
        foreach (var group in findings.GroupBy(item => item.Split(':')[^1]).OrderByDescending(group => group.Count()))
            output.WriteLine(group.Count() + " " + group.Key.Trim());
        foreach (var group in findings.GroupBy(item => item.Split(':')[0].Split('.')[3]).OrderByDescending(group => group.Count()))
            output.WriteLine("Domain findings " + group.Key + ": " + group.Count());
        foreach (var group in findings.GroupBy(item => (Domain: item.Split(':')[0].Split('.')[3], Issue: item.Split(':')[^1].Trim()))
                     .OrderBy(group => group.Key.Domain).ThenByDescending(group => group.Count()))
            output.WriteLine("Domain issue " + group.Key.Domain + " / " + group.Key.Issue + ": " + group.Count());
        foreach (var finding in findings)
            output.WriteLine(finding);
        output.WriteLine("Total findings: " + findings.Count);
    }
}
