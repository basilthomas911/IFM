using TomasAI.IFM.Application.Storage.ScheduledTaskDb.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ScheduledTaskDb;

[Collection("PostgreSQL plan atomicity")]
[Trait("Category", "SelfContained")]
public sealed class PostgresScheduledTaskTests(TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb.PostgresTradePlanFixture fixture)
{
    string Connection => fixture.Connection;
    async Task<ScheduledTaskDbContext> Store()
    {
        var settings = new DbConnectionSettings().Add(ScheduledTaskDbContext.ConnectionName, Connection, "System.Data.Postgres");
        await new ScheduledTaskSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        return new(settings, NullLogger<DbProvider>.Instance);
    }
    static ScheduledTaskRun Run(string environment, ScheduledTaskId schedule, DateTimeOffset fire) => new()
    {
        Id = new(Guid.NewGuid()), ScheduleId = schedule, Revision = 5, Environment = environment,
        HostId = "test-host", IntendedFireTimeUtc = fire, Status = ScheduledTaskRunStatus.Succeeded,
        CompletedEndOfDayValueDate = new DateOnly(2026, 10, 11), StandardOutputTail = "verified output"
    };

    [Fact]
    public async Task Run_and_completed_date_commit_together_and_stale_revisions_are_ignored()
    {
        var store = await Store(); var env = "test-" + Guid.NewGuid(); var schedule = new ScheduledTaskId(Guid.NewGuid());
        var run = Run(env, schedule, DateTimeOffset.UtcNow);
        await store.ProjectAsync(run);
        await store.ProjectAsync(run with { Revision = 4, StandardOutputTail = "stale" });
        var loaded = await store.GetRunAsync(env, run.HostId, schedule, run.Id, run.IntendedFireTimeUtc);
        loaded!.StandardOutputTail.Should().Be("verified output");
        (await store.GetCompletedEndOfDayAsync(env)).Should().Be(run.CompletedEndOfDayValueDate);
        var later = run with { Id = new(Guid.NewGuid()), IntendedFireTimeUtc = run.IntendedFireTimeUtc.AddDays(1), CompletedEndOfDayValueDate = new DateOnly(2026, 10, 12) };
        await store.ProjectAsync(later);
        await store.ProjectAsync(run);
        (await store.GetCompletedEndOfDayAsync(env)).Should().Be(later.CompletedEndOfDayValueDate);
    }

    [Fact]
    public async Task History_pages_all_rows_in_time_order_and_rejects_a_cursor_from_another_schedule()
    {
        var store = await Store(); var env = "test-" + Guid.NewGuid(); var schedule = new ScheduledTaskId(Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 151; i++) await store.ProjectAsync(Run(env, schedule, now.AddMinutes(-i)) with { CompletedEndOfDayValueDate = null });
        var results = new List<ScheduledTaskRun>(); byte[]? cursor = null; byte[]? firstCursor = null;
        do
        {
            var page = await store.GetRunHistoryAsync(env, "test-host", schedule, 40, cursor);
            results.AddRange(page.Runs); cursor = page.PagingState; firstCursor ??= cursor;
        } while (cursor is not null);
        results.Should().HaveCount(151); results.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        results.Select(x => x.IntendedFireTimeUtc).Should().BeInDescendingOrder();
        results.Should().OnlyContain(x => x.StandardOutputTail == "");
        await Assert.ThrowsAsync<ArgumentException>(async () => await store.GetRunHistoryAsync(env, "test-host", new(Guid.NewGuid()), 40, firstCursor));
    }

    [Fact]
    public async Task Completed_date_failure_rolls_back_the_run_row()
    {
        var store = await Store(); var env = "test-" + Guid.NewGuid(); var schedule = new ScheduledTaskId(Guid.NewGuid()); var run = Run(env, schedule, DateTimeOffset.UtcNow);
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(Connection); await connection.OpenAsync();
        var name = "test_eod_" + Guid.NewGuid().ToString("N");
        await using (var command = new NpgsqlCommand($"ALTER TABLE scheduled_task.futures_completed_end_of_day ADD CONSTRAINT {name} CHECK(environment <> '{env}');", connection)) await command.ExecuteNonQueryAsync();
        try
        {
            await Assert.ThrowsAsync<PostgresException>(async () => await store.ProjectAsync(run));
            (await store.GetRunAsync(env, run.HostId, schedule, run.Id, run.IntendedFireTimeUtc)).Should().BeNull();
            (await store.GetCompletedEndOfDayAsync(env)).Should().BeNull();
        }
        finally
        {
            await using var command = new NpgsqlCommand($"ALTER TABLE scheduled_task.futures_completed_end_of_day DROP CONSTRAINT {name};", connection); await command.ExecuteNonQueryAsync();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Source_event_run_and_completed_date_share_the_callers_transaction(bool failAfterProjection)
    {
        var store = await Store();
        var run = Run("enlisted-" + Guid.NewGuid(), new(Guid.NewGuid()), DateTimeOffset.UtcNow);
        var stream = "scheduled-atomic-" + Guid.NewGuid().ToString("N");
        var source = new TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.ScheduledTaskRunCompletedCompleteEvent
        {
            CommandId = Guid.NewGuid(), EntityId = run.Id, ScheduledTaskRun = run,
            AggregateId = run.Id.Value.ToString()
        };
        async Task Write() => await fixture.Transactions.ExecuteOnceAsync(async (transaction, token) =>
        {
            await transaction.AppendAsync(stream, source.CommandId, source, 0, token);
            await store.ProjectAsync(transaction, run, token);
            if (failAfterProjection) await transaction.ExecuteAsync("SELECT 1/0;", [], token);
            return true;
        });
        if (failAfterProjection) await Assert.ThrowsAsync<PostgresException>(Write);
        else await Write();
        (await fixture.EventCountAsync(stream)).Should().Be(failAfterProjection ? 0 : 1);
        var loaded = await store.GetRunAsync(run.Environment, run.HostId, run.ScheduleId, run.Id, run.IntendedFireTimeUtc);
        if (failAfterProjection)
        {
            loaded.Should().BeNull();
            (await store.GetCompletedEndOfDayAsync(run.Environment)).Should().BeNull();
        }
        else
        {
            loaded.Should().BeEquivalentTo(run);
            (await store.GetCompletedEndOfDayAsync(run.Environment)).Should().Be(run.CompletedEndOfDayValueDate);
        }
    }
}
