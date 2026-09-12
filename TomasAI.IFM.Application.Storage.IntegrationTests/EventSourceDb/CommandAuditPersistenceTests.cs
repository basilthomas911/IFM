using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.Storage.CommandAudit;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class CommandAuditPersistenceTests
{
    static string ConnectionString =>
        Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION")
        ?? "Host=localhost;Port=5432;Database=event-source-test-db";

    [Fact]
    public void MessagePack_codec_round_trips_without_compression_and_has_stable_identity()
    {
        var codec = new CommandAuditMessagePackCodec();
        var command = TestCommand.Create(Guid.NewGuid(), 42);

        var first = codec.Serialize(command);
        var second = codec.Serialize(command);
        var roundTrip = codec.Deserialize(typeof(TestCommand), first.Bytes, (short)first.Format, first.Version);

        first.Format.Should().Be(CommandAuditPayloadFormat.MessagePack);
        first.Version.Should().Be(CommandAuditMessagePackCodec.CurrentVersion);
        first.Bytes.Should().Equal(second.Bytes);
        first.Sha256.Should().Equal(second.Sha256);
        codec.Matches(command, first.Sha256).Should().BeTrue();
        roundTrip.Should().BeEquivalentTo(command);
    }

    [Fact]
    public void MessagePack_codec_rejects_unknown_format_and_version()
    {
        var codec = new CommandAuditMessagePackCodec();
        var payload = codec.Serialize(TestCommand.Create(Guid.NewGuid(), 1));

        FluentActions.Invoking(() => codec.Deserialize(typeof(TestCommand), payload.Bytes, 99, payload.Version))
            .Should().Throw<InvalidDataException>();
        FluentActions.Invoking(() => codec.Deserialize(typeof(TestCommand), payload.Bytes, (short)payload.Format, 99))
            .Should().Throw<InvalidDataException>();
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task Windowed_writer_reserves_once_and_detects_payload_conflicts()
    {
        await EnsureSchemaAsync();
        var commandId = Guid.NewGuid();
        var codec = new CommandAuditMessagePackCodec();
        var options = new CommandAuditPersistenceOptions
        {
            WriteMode = CommandAuditWriteMode.WindowedMessagePack,
            MaximumOldestRequestDelay = TimeSpan.FromMilliseconds(2)
        };
        await using var writer = new PostgresCommandAuditWriter(BaseConnectionString(), options);

        try
        {
            var accepted = await writer.ReserveAsync(CommandAuditEnvelope.Create(TestCommand.Create(commandId, 7), codec));
            var duplicate = await writer.ReserveAsync(CommandAuditEnvelope.Create(TestCommand.Create(commandId, 7), codec));

            accepted.Accepted.Should().BeTrue();
            duplicate.Accepted.Should().BeFalse();
            duplicate.LegacyConflict.Should().BeFalse();

            var conflicting = async () =>
                await writer.ReserveAsync(CommandAuditEnvelope.Create(TestCommand.Create(commandId, 8), codec));
            await conflicting.Should().ThrowAsync<CommandAuditPayloadConflictException>();

            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var read = new NpgsqlCommand(
                "SELECT commanddata, commandpayload, commandpayloadformat, commandpayloadversion, commandpayloadsha256 FROM command_log WHERE commandid=$1",
                connection);
            read.Parameters.AddWithValue(commandId);
            await using var reader = await read.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();
            reader.GetString(0).Should().BeEmpty();
            reader.GetFieldValue<byte[]>(1).Should().NotBeEmpty();
            reader.GetInt16(2).Should().Be((short)CommandAuditPayloadFormat.MessagePack);
            reader.GetInt32(3).Should().Be(CommandAuditMessagePackCodec.CurrentVersion);
            reader.GetFieldValue<byte[]>(4).Should().HaveCount(32);
        }
        finally
        {
            await DeleteAsync(commandId);
        }
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task Migration_preserves_legacy_json_rows()
    {
        await EnsureSchemaAsync();
        var commandId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        try
        {
            await using (var insert = new NpgsqlCommand(
                "INSERT INTO command_log(commandid,streamid,actorname,commandname,commandtimestamp,commandstatus,commanddata) VALUES($1,'legacy','Trade','Legacy','2026-01-01T00:00:00Z','InProgress','{\"value\":1}')",
                connection))
            {
                insert.Parameters.AddWithValue(commandId);
                await insert.ExecuteNonQueryAsync();
            }

            await using var read = new NpgsqlCommand(
                "SELECT commanddata,commandpayload,commandpayloadformat,commandpayloadversion,commandpayloadsha256 FROM command_log WHERE commandid=$1",
                connection);
            read.Parameters.AddWithValue(commandId);
            await using var reader = await read.ExecuteReaderAsync();
            (await reader.ReadAsync()).Should().BeTrue();
            reader.GetString(0).Should().Be("{\"value\":1}");
            for (var index = 1; index <= 4; index++) reader.IsDBNull(index).Should().BeTrue();
        }
        finally
        {
            await connection.CloseAsync();
            await DeleteAsync(commandId);
        }
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task One_database_window_accepts_only_the_first_identical_command_id_occurrence()
    {
        await EnsureSchemaAsync();
        var commandId = Guid.NewGuid();
        var codec = new CommandAuditMessagePackCodec();
        var envelope = CommandAuditEnvelope.Create(TestCommand.Create(commandId, 12), codec);

        try
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            var results = await CommandAuditPostgres.ReserveAsync(
                connection, transaction, new[] { envelope, envelope }, CancellationToken.None);
            await transaction.CommitAsync();

            results.Select(static result => result.Accepted).Should().Equal(true, false);
            results[1].LegacyConflict.Should().BeFalse();
        }
        finally
        {
            await DeleteAsync(commandId);
        }
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task Conflicting_payloads_for_one_command_id_roll_back_the_database_window()
    {
        await EnsureSchemaAsync();
        var commandId = Guid.NewGuid();
        var codec = new CommandAuditMessagePackCodec();
        var first = CommandAuditEnvelope.Create(TestCommand.Create(commandId, 12), codec);
        var conflicting = CommandAuditEnvelope.Create(TestCommand.Create(commandId, 13), codec);

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var reserve = async () => await CommandAuditPostgres.ReserveAsync(
                connection, transaction, new[] { first, conflicting }, CancellationToken.None);
            await reserve.Should().ThrowAsync<CommandAuditPayloadConflictException>();
            await transaction.RollbackAsync();
        }

        await using var read = new NpgsqlCommand("SELECT count(*) FROM command_log WHERE commandid=$1", connection);
        read.Parameters.AddWithValue(commandId);
        Convert.ToInt64(await read.ExecuteScalarAsync()).Should().Be(0);
    }

    static async Task EnsureSchemaAsync()
    {
        var settings = new DbConnectionSettings().Add(
            EventSourceActorDbContext.EventSourceActorDbConnection, BaseConnectionString(), "System.Data.Postgres");
        await new EventSourceSchemaDb(settings, Substitute.For<ILogger<DbProvider>>()).CreateAllAsync();
    }

    static string BaseConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString)
        {
            Username = string.Empty,
            Password = string.Empty
        };
        return builder.ConnectionString;
    }

    static async Task DeleteAsync(Guid commandId)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM command_log WHERE commandid=$1", connection);
        command.Parameters.AddWithValue(commandId);
        await command.ExecuteNonQueryAsync();
    }

    public sealed record TestCommand : ICommand
    {
        public required ActorSubject Subject { get; init; }
        public string CommandName => nameof(TestCommand);
        public BoundedContextName RouteTo => BoundedContextName.OptionTradeBoundedContext;
        public Guid CommandId { get; init; }
        public string StreamId => Subject.StreamId;
        public string EventSource => "Test";
        public int ErrorCode => 1;
        public int Value { get; init; }

        public static TestCommand Create(Guid commandId, int value) => new()
        {
            Subject = new ActorSubject(ActorType.Command, "Test", "Write", "1"),
            CommandId = commandId,
            Value = value
        };
    }
}
