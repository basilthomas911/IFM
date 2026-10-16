using System.Text;
using Newtonsoft.Json;
using FluentAssertions;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;

namespace TomasAI.IFM.Framework.Messaging.Nats.UnitTests;

public sealed class NatsJsonCompatibilityTests
{
    [Fact]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public void Existing_wire_metadata_reads_identically_without_type_activation()
    {
        var serializer = new NatsJsonDataSerializer();
        var expected = new Payload { Name = "accepted-source", Revision = 17 };
        var bytes = serializer.Serialize(expected);
        var json = Encoding.UTF8.GetString(bytes);
        json.Should().Contain("$type"); // Preserve the existing outgoing wire format.
        serializer.Deserialize<Payload>(bytes).Should().BeEquivalentTo(expected);
        JsonConvert.DeserializeObject<Payload>(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None })
            .Should().BeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("Unknown.Not.Installed, MissingAssembly")]
    [InlineData("System.IO.FileInfo, System.Private.CoreLib")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1707",Justification="Keep descriptive xUnit test names consistent with the repository test convention.")]
    public void Existing_reader_ignores_type_metadata_and_matches_explicit_none(string typeName)
    {
        var json = JsonConvert.SerializeObject(new Dictionary<string, object> { ["$type"] = typeName, ["Name"] = "trade", ["Revision"] = 9 });
        var serializer = new NatsJsonDataSerializer();
        var original = serializer.Deserialize<Payload>(Encoding.UTF8.GetBytes(json));
        var explicitNone = JsonConvert.DeserializeObject<Payload>(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None });
        original.Should().NotBeNull().And.BeEquivalentTo(explicitNone);
        original!.Name.Should().Be("trade");
        original.Revision.Should().Be(9);
    }
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1515",Justification="xUnit discovers public test classes and fixtures.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1034",Justification="These public nested types are concrete serializer test fixtures.")]

    public sealed class Payload
    {
        public string Name { get; init; } = string.Empty;
        public int Revision { get; init; }
    }
}
