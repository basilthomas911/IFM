using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Domain.Reference.Shared.Commands;

namespace TomasAI.IFM.Domain.Reference.UnitTests.Architecture;

/// <summary>Guards the published wire layouts of mapped Reference contracts outside ParameterSets.</summary>
public sealed class ReferenceMessageWireConventionTests
{
    public static TheoryData<Type> MessageTypes => new()
    {
        typeof(StrategyCatalogQuery),
        typeof(StrategyCatalogCommand),
        typeof(GetTradeStrategyFamiliesQuery),
        typeof(GetTradeStrategySymbolsQuery),
        typeof(GetLookupDefinitionsQuery),
        typeof(CreateRegimeDiscoveryParameterSetCommand),
        typeof(PublishRegimeDiscoveryParameterSetCommand),
        typeof(RetireRegimeDiscoveryParameterSetCommand),
        typeof(GetRegimeDiscoveryParameterSetQuery),
        typeof(ResolveRegimeDiscoveryParameterSetQuery),
        typeof(ChangeTradeStrategyFamilyCommand),
        typeof(CreateTradeStrategyFamilyCommand),
        typeof(RemoveTradeStrategyFamilyCommand),
    };

    [Theory, MemberData(nameof(MessageTypes))]
    public void Mapped_reference_contract_has_permanent_numeric_schema_and_round_trips(Type messageType)
    {
        var attribute = messageType.GetCustomAttribute<MessagePackObjectAttribute>();
        attribute.Should().NotBeNull();
        attribute!.AllowPrivate.Should().BeTrue();
        var properties = messageType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()?.IntKey))
            .Where(item => item.Key.HasValue)
            .OrderBy(item => item.Key)
            .ToArray();
        properties.Select(item => item.Key!.Value).Should().Equal(Enumerable.Range(0, properties.Length));
        var constructor = messageType.GetConstructors().Single(ctor => ctor.IsDefined(typeof(SerializationConstructorAttribute)));
        constructor.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(properties.Select(item => item.Property.PropertyType));
        var roundTrip = typeof(ReferenceMessageWireConventionTests).GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!;
        roundTrip.MakeGenericMethod(messageType).Invoke(null, null);
    }

    static void RoundTrip<T>() where T : new()
    {
        var bytes = MessagePackSerializer.Serialize(new T());
        var copy = MessagePackSerializer.Deserialize<T>(bytes);
        copy.Should().NotBeNull();
        MessagePackSerializer.Serialize(copy).Should().Equal(bytes);
    }
}
