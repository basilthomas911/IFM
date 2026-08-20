using System;
using FluentAssertions;
using TomasAI.IFM.Application.Storage.CommandLogBenchmark;
using TomasAI.IFM.Framework.Serialization;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.CommandLogBenchmark;

[Trait("Category", "Unit")]
public sealed class CommandLogBenchmarkContractTests
{
    [Fact]
    public void Scylla_guard_is_a_primary_key_lightweight_transaction()
    {
        CommandLogBenchmarkStatements.CreateScyllaTable.Should().Contain("commandId uuid PRIMARY KEY");
        CommandLogBenchmarkStatements.CreateScyllaTable.Should().Contain("commandData blob");
        CommandLogBenchmarkStatements.TryInsertScylla.Should().Contain("IF NOT EXISTS");
        CommandLogBenchmarkStatements.TryInsertScylla.Should().NotContain("ALLOW FILTERING");
    }

    [Fact]
    public void Entry_pre_serializes_equivalent_json_and_messagepack_payloads()
    {
        var timestamp = new DateTime(2026, 8, 20, 12, 30, 0, DateTimeKind.Local);
        var command = new TestCommand("ESU6", 17, true);

        var entry = CommandLogBenchmarkEntry.Create(
            Guid.NewGuid(),
            "stream-17",
            "MarketData",
            nameof(TestCommand),
            timestamp,
            command);

        entry.CommandTimestampUtc.Kind.Should().Be(DateTimeKind.Utc);
        entry.JsonCommandData.Should().Contain("ESU6");
        entry.MessagePackCommandData.Should().NotBeEmpty();
        new MessagePackBinarySerializer()
            .Deserialize<TestCommand>(entry.MessagePackCommandData)
            .Should().Be(command);
    }

    public sealed record TestCommand(string ContractId, int Sequence, bool Import);
}
