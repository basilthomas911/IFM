using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using TomasAI.IFM.Shared.EventModelActor;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public class EventRouteRegistryTests
{
    static readonly ActorTypeId Source =
        new(ActorType.Event, "SourceEventActor", "Completed");

    [Fact]
    public void Add_DeduplicatesDestinations_AndRemoveClearsLastRoute()
    {
        var registry = new EventRouteRegistry();
        var destination = new ActorMailboxId(ActorType.Event, "DestinationEventActor");

        registry.Add(Source, destination);
        registry.Add(Source, destination);

        registry.GetSnapshot(Source).Should().ContainSingle().Which.Should().Be(destination);

        registry.Remove(Source, destination);

        registry.GetSnapshot(Source).Should().BeEmpty();
    }

    [Fact]
    public void GetSnapshot_RemainsStableAfterLaterRouteChanges()
    {
        var registry = new EventRouteRegistry();
        var first = new ActorMailboxId(ActorType.Event, "FirstEventActor");
        var second = new ActorMailboxId(ActorType.Event, "SecondEventActor");
        registry.Add(Source, first);
        var snapshot = registry.GetSnapshot(Source);

        registry.Add(Source, second);
        registry.Remove(Source, first);

        snapshot.Should().BeEquivalentTo([first]);
        registry.GetSnapshot(Source).Should().BeEquivalentTo([second]);
    }

    [Fact]
    public async Task ConcurrentUpdates_AlwaysExposeValidImmutableSnapshots()
    {
        var registry = new EventRouteRegistry();
        var destinations = Enumerable.Range(0, 32)
            .Select(index => new ActorMailboxId(ActorType.Event, $"Route{index}"))
            .ToArray();

        await Task.WhenAll(destinations.Select((destination, index) => Task.Run(() =>
        {
            for (var iteration = 0; iteration < 250; iteration++)
            {
                registry.Add(Source, destination);
                var snapshot = registry.GetSnapshot(Source);
                snapshot.Should().OnlyHaveUniqueItems();
                if ((iteration + index) % 2 == 0)
                    registry.Remove(Source, destination);
            }
        })));

        registry.GetSnapshot(Source).Should().OnlyHaveUniqueItems();
    }
}
