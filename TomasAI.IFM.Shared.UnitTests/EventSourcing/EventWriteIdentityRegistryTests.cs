using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventSourcing;

public sealed class EventWriteIdentityRegistryTests
{
    [Fact]
    public void Default_writes_keep_the_exact_current_identity_and_reuse_metadata()
    {
        var writer = new EventWriteIdentityRegistry(new());
        var identity = writer.GetIdentity(typeof(UnknownEvent));
        Assert.Equal(typeof(UnknownEvent).Name, identity.EventName);
        Assert.Equal(typeof(UnknownEvent).AssemblyQualifiedName, identity.EventTypeName);
        Assert.Same(identity, writer.GetIdentity(typeof(UnknownEvent)));
    }

    [Fact]
    public void Explicit_legacy_identity_is_readable_and_cannot_be_rebound()
    {
        var reader = new EventTypeRegistry();
        var writer = new EventWriteIdentityRegistry(reader, useFrozenBaseline: false);
        const string legacy = "Legacy.Events.Observation, Removed.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
        _ = writer.GetIdentity(typeof(UnknownEvent));
        writer.RegisterLegacyIdentity(typeof(UnknownEvent), "Observation", legacy);
        writer.RegisterLegacyIdentity(typeof(UnknownEvent), "Observation", legacy);
        Assert.Equal(new("Observation", legacy), writer.GetIdentity(typeof(UnknownEvent)));
        Assert.Equal(typeof(UnknownEvent), reader.ResolveRequired(legacy));
        Assert.Throws<InvalidOperationException>(() => writer.RegisterLegacyIdentity(typeof(UnknownEvent), "Other", legacy));
        Assert.Throws<InvalidOperationException>(() => writer.RegisterLegacyIdentity(typeof(ErrorEvent), "Observation", legacy));
        Assert.Equal(typeof(UnknownEvent), reader.ResolveRequired(legacy));
    }

    [Theory]
    [InlineData("trade.observation/v1")]
    [InlineData("Unqualified")]
    [InlineData("")]
    public void This_phase_cannot_enable_stable_or_invalid_write_identities(string identity)
    {
        var reader = new EventTypeRegistry();
        var writer = new EventWriteIdentityRegistry(reader, useFrozenBaseline: false);
        Assert.ThrowsAny<ArgumentException>(() => writer.RegisterLegacyIdentity(typeof(UnknownEvent), "Observation", identity));
        Assert.Equal(typeof(UnknownEvent).AssemblyQualifiedName, writer.GetIdentity(typeof(UnknownEvent)).EventTypeName);
    }
}
