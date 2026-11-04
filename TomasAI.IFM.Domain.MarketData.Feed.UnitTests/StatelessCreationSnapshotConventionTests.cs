using System.Reflection;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests;

/// <summary>Prevents metadata-only replay from hiding newly introduced business state.</summary>
public sealed class StatelessCreationSnapshotConventionTests
{
    [Fact]
    public void Every_stateless_creation_state_declares_only_its_owner_identity()
    {
        var assembly = typeof(TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.State.FuturesTickDataCommandState).Assembly;
        var states = assembly.GetTypes().Where(type => !type.IsAbstract && typeof(IStatelessCreationSnapshotState).IsAssignableFrom(type)).ToArray();
        Assert.Equal(3, states.Length);
        foreach (var state in states)
        {
            var fields = state.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.All(fields, field => Assert.Equal("<Id>k__BackingField", field.Name));
            var properties = state.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.Single(properties);
            Assert.Equal("Id", properties[0].Name);
        }
    }
}
