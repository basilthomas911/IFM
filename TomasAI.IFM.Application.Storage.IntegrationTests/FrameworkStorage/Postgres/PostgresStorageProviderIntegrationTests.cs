using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Exceptions;
using Xunit;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage.Postgres;

[Collection(PostgresStorageProviderCollection.Name)]
[Trait("Category", "PostgresIntegration")]
public sealed class PostgresStorageProviderIntegrationTests(PostgresStorageProviderFixture fixture)
{
    const string InsertEventStream = """
        INSERT INTO event_stream_id (eventstreamid, eventstream)
        VALUES ($1, $2);
        """;

    const string InsertEventName = """
        INSERT INTO event_name_id (eventnameid, eventname, eventtypename)
        VALUES ($1, $2, $3);
        """;

    const string InsertEventLog = """
        INSERT INTO event_log (
            eventstreamid, eventnameid, eventversion, streamversion, eventdata, commandid, eventtimestamp)
        VALUES ($1, $2, $3, $4, $5, $6, $7);
        """;

    const string InsertCommandLog = """
        INSERT INTO command_log (
            commandid, streamid, actorname, commandname, commandtimestamp, commandstatus, commanddata)
        VALUES ($1, $2, $3, $4, $5, $6, $7);
        """;

    const string InsertProjectorState = """
        INSERT INTO event_projector_state (
            eventid, actorname, projectorname, isreplay, attemptnumber, outcome, stage,
            errormessage, createdtimestamp, updatedtimestamp, eventstreamid, sourceeventname, streamversion)
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13);
        """;

    const string SelectEventStream = """
        SELECT eventstreamid AS ignored_1, eventstream AS ignored_0
        FROM event_stream_id
        WHERE eventstreamid = $1;
        """;

    const string SelectEventLogs = """
        SELECT eventstreamid, eventnameid, eventversion, eventdata, commandid,
               eventtimestamp::timestamp
        FROM event_log
        WHERE eventstreamid = $1
        ORDER BY eventversion;
        """;

    [Fact]
    public Task ExecuteCommandAsync_WithoutParameters_ExecutesTextCommand()
    {
        var scope = PostgresEventSourceTestData.Scope(1);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var result = await repository.UseTest($"""
                    INSERT INTO event_stream_id (eventstreamid, eventstream)
                    VALUES ({scope.EventStreamId}, '{scope.EventStream}');
                    """)
                .ExecuteCommandAsync();

            Assert.Equal([1L], result);
            Assert.NotNull(await GetEventStreamAsync(repository, scope.EventStreamId));
        });
    }

    [Fact]
    public Task ExecuteCommandAsync_WithBindValue_InsertsOneRow()
    {
        var scope = PostgresEventSourceTestData.Scope(2);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var result = await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertEventStream)}", InsertEventStream)
                .SetParameters(new InsertEventStreamBindValue(scope.EventStreamId, scope.EventStream))
                .ExecuteCommandAsync();

            Assert.Equal([1L], result);
            var stream = await GetEventStreamAsync(repository, scope.EventStreamId);
            Assert.NotNull(stream);
            Assert.Equal(scope.EventStream, stream.EventStream);
        });
    }

    [Fact]
    public Task ExecuteCommandAsync_WithManyParameterValues_ExecutesEveryCommand()
    {
        var scope = PostgresEventSourceTestData.Scope(3);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var parameters = new[]
            {
                new EventStreamParameters(scope.EventStreamId, scope.EventStream),
                new EventStreamParameters(scope.SecondEventStreamId, scope.SecondEventStream)
            };

            var result = await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertEventStream)}", InsertEventStream)
                .SetParameters(parameters)
                .ExecuteCommandAsync();

            Assert.Equal([1L, 1L], result);
            Assert.NotNull(await GetEventStreamAsync(repository, scope.EventStreamId));
            Assert.NotNull(await GetEventStreamAsync(repository, scope.SecondEventStreamId));
        });
    }

    [Fact]
    public Task ExecuteCommandAsync_WithLargeDeferredSequence_StreamsBoundedBatches()
    {
        var scope = PostgresEventSourceTestData.Scope(3);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var prefix = $"__framework_storage_postgres_it__:{scope.Slot}:bulk:";
            var firstId = -1_800_000_000 + scope.Slot * 1_000;
            var enumerationCount = 0;
            try
            {
                var result = await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertEventStream)}", InsertEventStream)
                    .SetParameters(CreateParameters())
                    .ExecuteCommandAsync();

                var count = await repository.UseTest(
                        "SELECT count(*) FROM event_stream_id WHERE eventstream LIKE $1;")
                    .SetParameters(new BulkStreamPattern(prefix + "%"))
                    .ExecuteScalarAsync(static row => row.GetLong(0));

                Assert.Equal(256, result.Length);
                Assert.All(result, affected => Assert.Equal(1, affected));
                Assert.Equal(256, enumerationCount);
                Assert.Equal(256, count);
            }
            finally
            {
                await repository.UseTest("DELETE FROM event_stream_id WHERE eventstream LIKE $1;")
                    .SetParameters(new BulkStreamPattern(prefix + "%"))
                    .ExecuteCommandAsync();
            }

            IEnumerable<EventStreamParameters> CreateParameters()
            {
                for (var index = 0; index < 256; index++)
                {
                    enumerationCount++;
                    yield return new EventStreamParameters(firstId + index, $"{prefix}{index:D3}");
                }
            }
        });
    }

    [Fact]
    public Task ExecuteCommandAsync_WithCancelledToken_DoesNotEnumerateOrWrite()
    {
        var scope = PostgresEventSourceTestData.Scope(3);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var enumerationCount = 0;
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertEventStream)}", InsertEventStream)
                .SetParameters(CreateParameters())
                .ExecuteCommandAsync(cancellation.Token));

            Assert.Equal(0, enumerationCount);
            Assert.Null(await GetEventStreamAsync(repository, scope.EventStreamId));

            IEnumerable<EventStreamParameters> CreateParameters()
            {
                enumerationCount++;
                yield return new EventStreamParameters(scope.EventStreamId, scope.EventStream);
            }
        });
    }

    [Fact]
    public Task ExecuteCommandAsync_InAmbientTransaction_ExecutesMultipleCommandsUntilRollback()
    {
        var scope = PostgresEventSourceTestData.Scope(4);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var transaction = repository.BeginTransaction();
            Assert.NotNull(transaction);

            await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertEventStream)}", InsertEventStream)
                .SetParameters(new EventStreamParameters(scope.EventStreamId, scope.EventStream))
                .ExecuteCommandAsync();
            await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertEventStream)}", InsertEventStream)
                .SetParameters(new EventStreamParameters(scope.SecondEventStreamId, scope.SecondEventStream))
                .ExecuteCommandAsync();

            transaction.Rollback();

            Assert.Null(await GetEventStreamAsync(repository, scope.EventStreamId));
            Assert.Null(await GetEventStreamAsync(repository, scope.SecondEventStreamId));
        });
    }

    [Fact]
    public Task ExecuteQueuedCommandsAsync_WithFalseFlag_ExecutesEveryQueuedCommand()
    {
        var scope = PostgresEventSourceTestData.Scope(4);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var queuedCommands = new List<object>
            {
                repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertEventStream)}", InsertEventStream)
                    .SetParameters(new EventStreamParameters(scope.EventStreamId, scope.EventStream))
                    .QueueCommand(),
                repository.UseTest("UPDATE event_stream_id SET eventstream = $1 WHERE eventstreamid = $2;")
                    .SetParameters(new UpdateEventStreamParameters(scope.SecondEventStream, scope.EventStreamId))
                    .QueueCommand()
            };

            await repository.ExecuteQueuedCommandsAsync(queuedCommands, useTransaction: false);

            var stream = await GetEventStreamAsync(repository, scope.EventStreamId);
            Assert.NotNull(stream);
            Assert.Equal(scope.SecondEventStream, stream.EventStream);
        });
    }

    [Fact]
    public Task ExecuteQueuedCommandsAsync_WithTrueFlag_ExecutesEveryQueuedCommand()
    {
        var scope = PostgresEventSourceTestData.Scope(5);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var queuedCommands = new List<object>
            {
                repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertEventStream)}", InsertEventStream)
                    .SetParameters(new EventStreamParameters(scope.EventStreamId, scope.EventStream))
                    .QueueCommand(),
                repository.UseTest("UPDATE event_stream_id SET eventstream = $1 WHERE eventstreamid = $2;")
                    .SetParameters(new UpdateEventStreamParameters(scope.SecondEventStream, scope.EventStreamId))
                    .QueueCommand()
            };

            await repository.ExecuteQueuedCommandsAsync(queuedCommands, useTransaction: true);

            var stream = await GetEventStreamAsync(repository, scope.EventStreamId);
            Assert.NotNull(stream);
            Assert.Equal(scope.SecondEventStream, stream.EventStream);
        });
    }

    [Fact]
    public Task ExecuteQueryAsync_UsesOrdinalMapperForMultipleRows()
    {
        var scope = PostgresEventSourceTestData.Scope(6);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertEventLogsAsync(repository, scope);

            var rows = await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(SelectEventLogs)}", SelectEventLogs)
                .SetParameters(new EventStreamLookup(scope.EventStreamId))
                .ExecuteQueryAsync(MapEventLog);

            Assert.Equal(2, rows.Count);
            Assert.Equal(
                [scope.EventVersion, scope.SecondEventVersion],
                rows.Select(row => row.EventVersion).ToArray());
        });
    }

    [Fact]
    public Task ExecuteQueryImmutableAsync_ReturnsOwnedValueTypeOrdinalResults()
    {
        var scope = PostgresEventSourceTestData.Scope(7);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertEventLogsAsync(repository, scope);

            var result = await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(SelectEventLogs)}", SelectEventLogs)
                .SetParameters(new EventStreamLookup(scope.EventStreamId))
                .ExecuteQueryImmutableAsync(static row => new ImmutableEventRow(
                    row.GetLong(0), row.GetLong(2), row.GetGuid(4)));
            var rows = Assert.IsType<ImmutableEventRow[]>(result);

            Assert.Equal(2, rows.Length);
            Assert.Equal(scope.EventStreamId, rows[0].EventStreamId);
        });
    }

    [Fact]
    public Task ExecuteSingleAsync_ReturnsMappedRowOrNull()
    {
        var scope = PostgresEventSourceTestData.Scope(8);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertEventStreamAsync(repository, scope);

            var existing = await GetEventStreamAsync(repository, scope.EventStreamId);
            var missing = await GetEventStreamAsync(repository, scope.EventStreamId - 1000);

            Assert.NotNull(existing);
            Assert.Equal(scope.EventStream, existing.EventStream);
            Assert.Null(missing);
        });
    }

    [Fact]
    public Task ExecuteScalarAsync_MapsFirstColumnByOrdinal()
    {
        var scope = PostgresEventSourceTestData.Scope(9);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertEventStreamAsync(repository, scope);

            var eventStreamId = await repository
                .UseTest("SELECT eventstreamid AS deliberately_not_value FROM event_stream_id WHERE eventstreamid = $1;")
                .SetParameters(new EventStreamLookup(scope.EventStreamId))
                .ExecuteScalarAsync(static row => row.GetInt(0));

            Assert.Equal(scope.EventStreamId, eventStreamId);
        });
    }

    [Fact]
    public Task ParameterizedTextCommand_IsExplicitlyPreparedOnThePooledConnection()
    {
        var scope = PostgresEventSourceTestData.Scope(9);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            const string preparedSql = """
                SELECT $1::integer,
                       EXISTS (
                           SELECT 1
                           FROM pg_prepared_statements
                           WHERE statement LIKE '%framework_storage_explicit_prepare_probe%')
                /* framework_storage_explicit_prepare_probe */;
                """;
            var result = await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(preparedSql)}", preparedSql)
                .SetParameters(new EventStreamLookup(scope.EventStreamId))
                .ExecuteSingleAsync(static row => (Value: row.GetInt(0), IsPrepared: row.GetBool(1)));

            Assert.Equal(scope.EventStreamId, result.Value);
            Assert.True(result.IsPrepared);
        });
    }

    [Fact]
    public Task ExecuteMapReduceAsync_StreamsOrdinalResultsIntoReducer()
    {
        var scope = PostgresEventSourceTestData.Scope(10);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertEventLogsAsync(repository, scope);
            var reducerCalls = 0;
            var versionSum = 0L;

            await repository.UseTest("SELECT eventversion FROM event_log WHERE eventstreamid = $1 ORDER BY eventversion;")
                .SetParameters(new EventStreamLookup(scope.EventStreamId))
                .ExecuteMapReduceAsync(
                    static row => row.GetLong(0),
                    rows =>
                    {
                        reducerCalls++;
                        versionSum = rows.Sum();
                    });

            Assert.Equal(1, reducerCalls);
            Assert.Equal(scope.EventVersion + scope.SecondEventVersion, versionSum);
        });
    }

    [Fact]
    public Task ExecuteStreamAsync_StreamsEveryOrdinalResult()
    {
        var scope = PostgresEventSourceTestData.Scope(16);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertEventLogsAsync(repository, scope);
            var versions = new List<long>();

            await foreach (var row in repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(SelectEventLogs)}", SelectEventLogs)
                .SetParameters(new EventStreamLookup(scope.EventStreamId))
                .ExecuteStreamAsync(MapEventLog))
            {
                versions.Add(row.EventVersion);
            }

            Assert.Equal([scope.EventVersion, scope.SecondEventVersion], versions);
        });
    }

    [Fact]
    public Task ExecuteStreamAsync_EarlyTerminationReleasesReader()
    {
        var scope = PostgresEventSourceTestData.Scope(16);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertEventLogsAsync(repository, scope);
            var stream = repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(SelectEventLogs)}", SelectEventLogs)
                .SetParameters(new EventStreamLookup(scope.EventStreamId))
                .ExecuteStreamAsync(MapEventLog);

            await using (var rows = stream.GetAsyncEnumerator())
            {
                Assert.True(await rows.MoveNextAsync());
                Assert.Equal(scope.EventVersion, rows.Current.EventVersion);
            }

            var count = await repository
                .UseTest("SELECT count(*) FROM event_log WHERE eventstreamid = $1;")
                .SetParameters(new EventStreamLookup(scope.EventStreamId))
                .ExecuteScalarAsync(static row => row.GetLong(0));

            Assert.Equal(2, count);
        });
    }

    [Fact]
    public Task ExecuteStreamAsync_CancellationStopsEnumerationAndReleasesReader()
    {
        var scope = PostgresEventSourceTestData.Scope(16);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertEventLogsAsync(repository, scope);
            using var cancellation = new CancellationTokenSource();
            var rowsRead = 0;

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await foreach (var _ in repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(SelectEventLogs)}", SelectEventLogs)
                    .SetParameters(new EventStreamLookup(scope.EventStreamId))
                    .ExecuteStreamAsync(MapEventLog, cancellation.Token))
                {
                    rowsRead++;
                    cancellation.Cancel();
                }
            });

            Assert.Equal(1, rowsRead);
            var count = await repository
                .UseTest("SELECT count(*) FROM event_log WHERE eventstreamid = $1;")
                .SetParameters(new EventStreamLookup(scope.EventStreamId))
                .ExecuteScalarAsync(static row => row.GetLong(0));
            Assert.Equal(2, count);
        });
    }

    [Fact]
    public Task OrdinalMapping_ReadsEveryEventSourceTableAndCorePostgresType()
    {
        var scope = PostgresEventSourceTestData.Scope(11);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            await InsertCompleteEventSourceGraphAsync(repository, scope);

            var stream = await GetEventStreamAsync(repository, scope.EventStreamId);
            var eventName = await repository.UseTest("""
                    SELECT eventnameid AS ignored_2, eventname AS ignored_1, eventtypename AS ignored_0
                    FROM event_name_id WHERE eventnameid = $1;
                    """)
                .SetParameters(new EventNameLookup(scope.EventNameId))
                .ExecuteSingleAsync(static row => new EventNameRow(
                    row.GetInt(0), row.GetString(1), row.GetString(2)));
            var eventLog = await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(SelectEventLogs)}", SelectEventLogs)
                .SetParameters(new EventStreamLookup(scope.EventStreamId))
                .ExecuteSingleAsync(MapEventLog);
            var command = await repository.UseTest("""
                    SELECT commandid, streamid, actorname, commandname, commandtimestamp::timestamp,
                           commandstatus, commanddata
                    FROM command_log WHERE commandid = $1;
                    """)
                .SetParameters(new CommandLookup(scope.CommandId))
                .ExecuteSingleAsync(static row => new CommandRow(
                    row.GetGuid(0),
                    row.GetString(1),
                    row.GetString(2),
                    row.GetString(3),
                    row.GetDateTime(4),
                    row.GetEnum<TestCommandStatus>(5),
                    row.GetString(6)));
            var projector = await repository.UseTest("""
                    SELECT eventid, actorname, projectorname, isreplay, attemptnumber, outcome, stage,
                           errormessage, createdtimestamp::timestamp, updatedtimestamp::timestamp
                    FROM event_projector_state WHERE eventid = $1 AND projectorname = $2;
                    """)
                .SetParameters(new ProjectorLookup(scope.EventVersion, scope.ProjectorName))
                .ExecuteSingleAsync(static row => new ProjectorRow(
                    row.GetLong(0),
                    row.GetString(1),
                    row.GetString(2),
                    row.GetBool(3),
                    row.GetInt(4),
                    row.GetEnum<TestProjectorOutcome>(5),
                    row.GetString(6),
                    row.GetString(7),
                    row.GetDateTime(8),
                    row.GetDateTime(9)));

            Assert.NotNull(stream);
            Assert.Equal((scope.EventStreamId, scope.EventStream), (stream.EventStreamId, stream.EventStream));

            Assert.NotNull(eventName);
            Assert.Equal((scope.EventNameId, scope.EventName, scope.EventTypeName),
                (eventName.EventNameId, eventName.EventName, eventName.EventTypeName));

            Assert.NotNull(eventLog);
            Assert.Equal(scope.EventVersion, eventLog.EventVersion);
            Assert.Equal(scope.CommandId, eventLog.CommandId);
            Assert.Equal(new DateTime(2026, 1, 15, 13, 30, 0).Ticks, eventLog.EventTimestamp.Ticks);

            Assert.NotNull(command);
            Assert.Equal(TestCommandStatus.Completed, command.Status);
            Assert.Equal(scope.CommandId, command.CommandId);

            Assert.NotNull(projector);
            Assert.True(projector.IsReplay);
            Assert.Equal(3, projector.AttemptNumber);
            Assert.Equal(TestProjectorOutcome.Completed, projector.Outcome);
        });
    }

    [Fact]
    public Task QueryMethods_RejectMoreThanOneParameterValue()
    {
        var scope = PostgresEventSourceTestData.Scope(12);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var context = repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(SelectEventStream)}", SelectEventStream)
                .SetParameters(new[]
                {
                    new EventStreamLookup(scope.EventStreamId),
                    new EventStreamLookup(scope.SecondEventStreamId)
                });

            var exception = await Assert.ThrowsAsync<StorageException>(
                () => context.ExecuteQueryAsync(MapEventStream));

            Assert.Contains("only single parameter value accepted", exception.Message);
        });
    }

    [Fact]
    public Task ExecuteQueuedCommandsAsync_RejectsEmptyQueue()
    {
        var scope = PostgresEventSourceTestData.Scope(13);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var context = repository.UseTest("SELECT 1;");
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.ExecuteQueuedCommandsAsync([]));

            Assert.Contains("no commands have been queued", exception.Message);
        });
    }

    [Fact]
    public Task MappingMethods_RejectNullDelegates()
    {
        var scope = PostgresEventSourceTestData.Scope(14);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            Assert.Throws<ArgumentNullException>(
                () => repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(SelectEventStream)}", SelectEventStream).ExecuteStreamAsync<EventStreamRow>(null!));
            await Assert.ThrowsAsync<StorageException>(
                () => repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(SelectEventStream)}", SelectEventStream).ExecuteQueryAsync<EventStreamRow>(null!));
            await Assert.ThrowsAsync<StorageException>(
                () => repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(SelectEventStream)}", SelectEventStream).ExecuteSingleAsync<EventStreamRow>(null!));
            await Assert.ThrowsAsync<StorageException>(
                () => repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(SelectEventStream)}", SelectEventStream).ExecuteQueryImmutableAsync<ImmutableEventRow>(null!));
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(SelectEventStream)}", SelectEventStream).ExecuteMapReduceAsync<int>(null!, _ => { }));
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(SelectEventStream)}", SelectEventStream).ExecuteMapReduceAsync(static row => row.GetInt(0), null!));
        });
    }

    [Fact]
    public Task ExecuteQueuedCommandsAsync_RollsBackEarlierCommandsWhenLaterCommandFails()
    {
        var scope = PostgresEventSourceTestData.Scope(15);
        return fixture.RunIsolatedAsync(scope, async repository =>
        {
            var queuedCommands = new List<object>
            {
                repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertEventStream)}", InsertEventStream)
                    .SetParameters(new EventStreamParameters(scope.EventStreamId, scope.EventStream))
                    .QueueCommand(),
                repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertEventStream)}", InsertEventStream)
                    .SetParameters(new EventStreamParameters(scope.EventStreamId, scope.SecondEventStream))
                    .QueueCommand()
            };

            await Assert.ThrowsAsync<StorageException>(
                () => repository.ExecuteQueuedCommandsAsync(queuedCommands, useTransaction: true));

            Assert.Null(await GetEventStreamAsync(repository, scope.EventStreamId));
        });
    }

    static Task InsertEventStreamAsync(PostgresTestRepository repository, PostgresEventSourceTestScope scope)
        => repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertEventStream)}", InsertEventStream)
            .SetParameters(new EventStreamParameters(scope.EventStreamId, scope.EventStream))
            .ExecuteCommandAsync();

    static async Task InsertEventLogsAsync(PostgresTestRepository repository, PostgresEventSourceTestScope scope)
    {
        await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertEventLog)}", InsertEventLog)
            .SetParameters(new[]
            {
                CreateEventLogParameters(scope, scope.EventVersion, "{\"index\":1}"),
                CreateEventLogParameters(scope, scope.SecondEventVersion, "{\"index\":2}")
            })
            .ExecuteCommandAsync();
    }

    static async Task InsertCompleteEventSourceGraphAsync(
        PostgresTestRepository repository,
        PostgresEventSourceTestScope scope)
    {
        await InsertEventStreamAsync(repository, scope);
        await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertEventName)}", InsertEventName)
            .SetParameters(new EventNameParameters(scope.EventNameId, scope.EventName, scope.EventTypeName))
            .ExecuteCommandAsync();
        await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertEventLog)}", InsertEventLog)
            .SetParameters(CreateEventLogParameters(scope, scope.EventVersion, "{\"ordinal\":true}"))
            .ExecuteCommandAsync();
        await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertCommandLog)}", InsertCommandLog)
            .SetParameters(new CommandParameters(
                scope.CommandId,
                scope.EventStream,
                "FundActor",
                "CreateFund",
                PostgresEventSourceTestData.Timestamp,
                "Completed",
                "{\"fundId\":1}"))
            .ExecuteCommandAsync();
        await repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(InsertProjectorState)}", InsertProjectorState)
            .SetParameters(new ProjectorParameters(
                scope.EventVersion,
                "FundActor",
                scope.ProjectorName,
                true,
                3,
                "Completed",
                "Projection",
                string.Empty,
                PostgresEventSourceTestData.Timestamp,
                PostgresEventSourceTestData.UpdatedTimestamp,
                scope.EventStreamId,
                scope.EventName,
                1))
            .ExecuteCommandAsync();
    }

    static EventLogParameters CreateEventLogParameters(
        PostgresEventSourceTestScope scope,
        long eventVersion,
        string eventData)
        => new(
            scope.EventStreamId,
            scope.EventNameId,
            eventVersion,
            eventVersion == scope.EventVersion ? 1 : 2,
            eventData,
            scope.CommandId,
            PostgresEventSourceTestData.Timestamp);

    static Task<EventStreamRow?> GetEventStreamAsync(PostgresTestRepository repository, int eventStreamId)
        => repository.Use($"{nameof(PostgresStorageProviderIntegrationTests)}.{nameof(SelectEventStream)}", SelectEventStream)
            .SetParameters(new EventStreamLookup(eventStreamId))
            .ExecuteSingleAsync(MapEventStream);

    static EventStreamRow MapEventStream(IObjectDataRecord row)
        => new(row.GetInt(0), row.GetString(1));

    static EventLogRow MapEventLog(IObjectDataRecord row)
        => new(
            row.GetLong(0),
            row.GetInt(1),
            row.GetLong(2),
            row.GetString(3),
            row.GetGuid(4),
            row.GetDateTime(5));

    readonly record struct InsertEventStreamBindValue(int EventStreamId, string EventStream) : IBindValue
    {
        public object Bind() => Values(Integer(EventStreamId), Text(EventStream));
    }

    readonly record struct EventStreamParameters(int EventStreamId, string EventStream) : IBindValue
    {
        public object Bind() => Values(Integer(EventStreamId), Text(EventStream));
    }
    readonly record struct UpdateEventStreamParameters(string EventStream, int EventStreamId) : IBindValue
    {
        public object Bind() => Values(Text(EventStream), Integer(EventStreamId));
    }
    readonly record struct EventStreamLookup(int EventStreamId) : IBindValue
    {
        public object Bind() => Values(Integer(EventStreamId));
    }
    readonly record struct BulkStreamPattern(string Pattern) : IBindValue
    {
        public object Bind() => Values(Text(Pattern));
    }
    readonly record struct EventNameLookup(int EventNameId) : IBindValue
    {
        public object Bind() => Values(Integer(EventNameId));
    }
    readonly record struct CommandLookup(Guid CommandId) : IBindValue
    {
        public object Bind() => Values(Uuid(CommandId));
    }
    readonly record struct ProjectorLookup(long EventId, string ProjectorName) : IBindValue
    {
        public object Bind() => Values(Bigint(EventId), Text(ProjectorName));
    }

    readonly record struct EventNameParameters(int EventNameId, string EventName, string EventTypeName) : IBindValue
    {
        public object Bind() => Values(Integer(EventNameId), Text(EventName), Text(EventTypeName));
    }

    readonly record struct EventLogParameters(
        int EventStreamId,
        int EventNameId,
        long EventVersion,
        long StreamVersion,
        string EventData,
        Guid CommandId,
        string EventTimestamp) : IBindValue
    {
        public object Bind() => Values(
            Integer(EventStreamId), Integer(EventNameId), Bigint(EventVersion), Bigint(StreamVersion),
            Text(EventData), Uuid(CommandId), Text(EventTimestamp));
    }

    readonly record struct CommandParameters(
        Guid CommandId,
        string StreamId,
        string ActorName,
        string CommandName,
        string CommandTimestamp,
        string CommandStatus,
        string CommandData) : IBindValue
    {
        public object Bind() => Values(
            Uuid(CommandId), Text(StreamId), Text(ActorName), Text(CommandName), Text(CommandTimestamp),
            Text(CommandStatus), Text(CommandData));
    }

    readonly record struct ProjectorParameters(
        long EventId,
        string ActorName,
        string ProjectorName,
        bool IsReplay,
        int AttemptNumber,
        string Outcome,
        string Stage,
        string ErrorMessage,
        string CreatedTimestamp,
        string UpdatedTimestamp,
        long EventStreamId,
        string SourceEventName,
        long StreamVersion) : IBindValue
    {
        public object Bind() => Values(
            Bigint(EventId), Text(ActorName), Text(ProjectorName), Boolean(IsReplay), Integer(AttemptNumber),
            Text(Outcome), Text(Stage), Text(ErrorMessage), Text(CreatedTimestamp), Text(UpdatedTimestamp),
            Bigint(EventStreamId), Text(SourceEventName), Bigint(StreamVersion));
    }

    sealed record EventStreamRow(int EventStreamId, string EventStream);
    sealed record EventNameRow(int EventNameId, string EventName, string EventTypeName);

    sealed record EventLogRow(
        long EventStreamId,
        int EventNameId,
        long EventVersion,
        string EventData,
        Guid CommandId,
        DateTime EventTimestamp);

    readonly record struct ImmutableEventRow(long EventStreamId, long EventVersion, Guid CommandId);

    sealed record CommandRow(
        Guid CommandId,
        string StreamId,
        string ActorName,
        string CommandName,
        DateTime CommandTimestamp,
        TestCommandStatus Status,
        string CommandData);

    sealed record ProjectorRow(
        long EventId,
        string ActorName,
        string ProjectorName,
        bool IsReplay,
        int AttemptNumber,
        TestProjectorOutcome Outcome,
        string Stage,
        string ErrorMessage,
        DateTime CreatedTimestamp,
        DateTime UpdatedTimestamp);

    enum TestCommandStatus
    {
        Unknown,
        Completed
    }

    enum TestProjectorOutcome
    {
        Unknown,
        Completed
    }
}
