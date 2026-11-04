using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;
using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Events;
using TomasAI.IFM.Shared.EventSourcing;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.Actor;
using TomasAI.IFM.Domain.Reference.Configuration.Strategy.Command.EventProjector;
using TomasAI.IFM.Domain.Reference.Shared.ProjectionOutcomes;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Reference.UnitTests.Architecture;

/// <summary>Guards the published wire layouts of mapped Reference contracts outside ParameterSets.</summary>
public sealed class ReferenceMessageWireConventionTests
{
    [Fact]
    public void Projection_terminal_events_preserve_source_correlation_through_messagepack()
    {
        var context = Substitute.For<IRegimeDiscoveryConfigurationCommandContext>();
        var projector = new RegimeDiscoveryConfigurationEventProjector(context);
        var entityId = new RegimeDiscoveryParameterSetEntityId(Guid.NewGuid(), 1);
        var source = new RegimeDiscoveryConfigurationParameterSetPublishedEvent
        {
            Subject = new(ActorType.Event, "RegimeDiscoveryConfiguration", "Published", entityId.Format()),
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EventId = 42, EntityId = entityId
        };
        var descriptor = Assert.Single(projector.ProjectionDescriptors,
            item => item.SourceEventType == source.GetType());
        var complete = Assert.IsType<RegimeDiscoveryConfigurationParameterSetPublishedCompleteEvent>(
            descriptor.CompletedEventFactory(source));
        var fail = Assert.IsType<RegimeDiscoveryConfigurationParameterSetPublishedFailEvent>(
            descriptor.FailedEventFactory(source, new InvalidOperationException("Projection failed.")));

        var completedCopy = MessagePackSerializer.Deserialize<RegimeDiscoveryConfigurationParameterSetPublishedCompleteEvent>(
            MessagePackSerializer.Serialize(complete));
        var failedCopy = MessagePackSerializer.Deserialize<RegimeDiscoveryConfigurationParameterSetPublishedFailEvent>(
            MessagePackSerializer.Serialize(fail));
        Assert.Equal(source.CommandId, completedCopy.CommandId);
        Assert.Equal(source.EventId, completedCopy.EventId);
        Assert.Equal(entityId, completedCopy.EntityId);
        Assert.Equal(source.CommandId, failedCopy.CommandId);
        Assert.Equal(ErrorType.Denormalizer, failedCopy.ErrorType);
        Assert.Equal("Projection failed.", failedCopy.ErrorMessage);
    }

    [Theory]
    [InlineData(typeof(ParameterSetDraftSavedEvent), "ParameterDraftSavedEvent")]
    [InlineData(typeof(ParameterSetVersionPublishedEvent), "ParameterVersionPublishedEvent")]
    [InlineData(typeof(ParameterSetVersionRetiredEvent), "ParameterVersionRetiredEvent")]
    [InlineData(typeof(RegimeDiscoveryConfigurationParameterSetCreatedEvent), "RegimeDiscoveryParameterSetCreatedEvent")]
    [InlineData(typeof(RegimeDiscoveryConfigurationParameterSetPublishedEvent), "RegimeDiscoveryParameterSetPublishedEvent")]
    [InlineData(typeof(RegimeDiscoveryConfigurationParameterSetRetiredEvent), "RegimeDiscoveryParameterSetRetiredEvent")]
    public void Renamed_events_preserve_their_persisted_identity_and_snapshot_aliases(Type eventType, string historicalName)
    {
        var reader = EventTypeRegistry.CreateCompatibilityRegistry();
        var writer = new EventWriteIdentityRegistry(reader);
        var identity = writer.GetIdentity(eventType);
        Assert.Equal(historicalName, identity.EventName);
        Assert.Equal(eventType, reader.Resolve(identity.EventTypeName));
        Assert.Contains(identity.EventTypeName, reader.GetReadIdentities(eventType));
        var currentAssemblyAlias = $"{eventType.Namespace}.{historicalName}, {eventType.Assembly.GetName().Name}";
        Assert.Equal(eventType, reader.Resolve(currentAssemblyAlias));
        Assert.Equal(historicalName, ((IEvent)Activator.CreateInstance(eventType)!).EventName);
    }

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
