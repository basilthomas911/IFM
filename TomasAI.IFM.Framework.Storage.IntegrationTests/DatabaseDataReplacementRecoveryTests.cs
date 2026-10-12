using NSubstitute;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Processing;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.LocalWorkstation.Recovery;

namespace TomasAI.IFM.Framework.Storage.IntegratedTests;

public sealed class DatabaseDataReplacementRecoveryTests : IDisposable
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), "ifm-replacement-tests", Guid.NewGuid().ToString("N"));
    readonly IDatabaseReplacementRecoveryPlatform _platform = Substitute.For<IDatabaseReplacementRecoveryPlatform>();
    readonly DatabaseCutoverRequest _request = new(new(Guid.NewGuid()), new("core-postgresql"),
        DatabaseEngine.PostgreSql, new("verified-backup"), new("fresh-validation", "disposable"), 12, 1);
    readonly List<string> _calls = [];

    public DatabaseDataReplacementRecoveryTests()
    {
        _platform.PrepareAsync(Arg.Any<DatabaseCutoverRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => { _calls.Add("prepare"); return ValueTask.FromResult("preserved-original-data"); });
        Configure("stop", () => _platform.StopApplicationAsync(_request, Arg.Any<CancellationToken>()));
        Configure("replace", () => _platform.ReplaceDataAsync(_request, "preserved-original-data", Arg.Any<CancellationToken>()));
        Configure("database", () => _platform.ValidateDatabaseAsync(_request, Arg.Any<CancellationToken>()));
        Configure("start-fenced", () => _platform.StartApplicationInRecoveryAsync(_request, Arg.Any<CancellationToken>()));
        Configure("application", () => _platform.ValidateApplicationAsync(_request, Arg.Any<CancellationToken>()));
        Configure("resume", () => _platform.ResumeAdmissionAsync(_request, Arg.Any<CancellationToken>()));
        Configure("rollback", () => _platform.RollbackAsync(_request, "preserved-original-data", Arg.Any<CancellationToken>()));
        Configure("rollback-health", () => _platform.ValidateRollbackAsync(_request, Arg.Any<CancellationToken>()));
    }

    void Configure(string name, Func<ValueTask> configure)
        => configure().Returns(_ => { _calls.Add(name); return ValueTask.CompletedTask; });

    [Fact]
    public async Task Completion_requires_database_and_application_validation_before_admission()
    {
        var result = await Coordinator().RecoverAsync(_request, new Progress<DatabaseNativeProgress>(), default);
        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "prepare", "stop", "replace", "database", "start-fenced", "application", "resume" }, _calls);
        var completed = await ReadAsync(_request);
        Assert.Equal(DatabaseReplacementStage.Completed, completed!.Stage);
        Assert.Equal(_request, completed.Request); // Exercises immutable ID serialization.
    }

    [Fact]
    public async Task Reconciled_prepared_record_reuses_cold_data_and_runs_all_health_gates()
    {
        await using (var lease = await Journal().AcquireAsync(_request, default))
            await lease.WriteAsync(new(_request, DatabaseReplacementStage.Prepared, "preserved-original-data"), default);
        var result = await Coordinator().RecoverAsync(_request, new Progress<DatabaseNativeProgress>(), default);
        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "stop", "replace", "database", "start-fenced", "application", "resume" }, _calls);
        Assert.Equal(DatabaseReplacementStage.Completed, (await ReadAsync(_request))!.Stage);
    }
    [Fact]
    public async Task Reconciled_admission_recovery_validates_current_data_without_replacement()
    {
        await using (var lease = await Journal().AcquireAsync(_request, default))
            await lease.WriteAsync(new(_request, DatabaseReplacementStage.ValidatingApplication, "preserved-original-data"), default);
        var result = await Coordinator().RecoverAsync(_request, new Progress<DatabaseNativeProgress>(), default);
        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "stop", "database", "start-fenced", "application", "resume" }, _calls);
        Assert.Equal(DatabaseReplacementStage.Completed, (await ReadAsync(_request))!.Stage);
    }

    [Fact]
    public async Task Admission_recovery_failure_cannot_rollback_writes_that_may_have_resumed()
    {
        await using (var lease = await Journal().AcquireAsync(_request, default))
            await lease.WriteAsync(new(_request, DatabaseReplacementStage.ValidatingApplication, "preserved-original-data"), default);
        FailAt("database");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Coordinator().RecoverAsync(_request, new Progress<DatabaseNativeProgress>(), default).AsTask());
        Assert.DoesNotContain("replace", _calls);
        Assert.DoesNotContain("rollback", _calls);
        Assert.Equal(DatabaseReplacementStage.ReconciliationRequired, (await ReadAsync(_request))!.Stage);
    }
    [Theory]
    [InlineData("stop")]
    [InlineData("replace")]
    [InlineData("database")]
    [InlineData("start-fenced")]
    [InlineData("application")]
    public async Task Failure_before_admission_restores_and_validates_original_data(string failingStep)
    {
        FailAt(failingStep);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Coordinator().RecoverAsync(_request, new Progress<DatabaseNativeProgress>(), default).AsTask());
        Assert.DoesNotContain("resume", _calls);
        Assert.Equal(new[] { "rollback", "rollback-health" }, _calls.TakeLast(2));
        Assert.Equal(DatabaseReplacementStage.RolledBack, (await ReadAsync(_request))!.Stage);
    }

    [Fact]
    public async Task Lost_admission_reply_cannot_trigger_rollback_of_new_writes()
    {
        FailAt("resume");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Coordinator().RecoverAsync(_request, new Progress<DatabaseNativeProgress>(), default).AsTask());
        Assert.DoesNotContain("rollback", _calls);
        Assert.Equal(DatabaseReplacementStage.ReconciliationRequired, (await ReadAsync(_request))!.Stage);
    }

    [Fact]
    public async Task Restart_returns_durable_completion_without_replacing_data_again()
    {
        var original = await Coordinator().RecoverAsync(_request, new Progress<DatabaseNativeProgress>(), default);
        var replay = await Coordinator().RecoverAsync(_request with { FencingToken = 2 }, new Progress<DatabaseNativeProgress>(), default);
        Assert.Equal(original, replay);
        Assert.Equal(1, _calls.Count(x => x == "replace"));
    }

    [Fact]
    public async Task Crash_record_blocks_same_operation_and_other_database_replacement()
    {
        await using (var lease = await Journal().AcquireAsync(_request, default))
            await lease.WriteAsync(new(_request, DatabaseReplacementStage.ReplacingData, "original"), default);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Coordinator().RecoverAsync(_request, new Progress<DatabaseNativeProgress>(), default).AsTask());
        var other = _request with { OperationId = new(Guid.NewGuid()) };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Coordinator().RecoverAsync(other, new Progress<DatabaseNativeProgress>(), default).AsTask());
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task A_lower_fence_or_different_restore_cannot_reuse_completion()
    {
        await Coordinator().RecoverAsync(_request with { FencingToken = 2 }, new Progress<DatabaseNativeProgress>(), default);
        await Assert.ThrowsAsync<DatabaseExecutionConflictException>(() =>
            Coordinator().RecoverAsync(_request, new Progress<DatabaseNativeProgress>(), default).AsTask());
        await Assert.ThrowsAsync<DatabaseExecutionConflictException>(() =>
            Coordinator().RecoverAsync(_request with { RestorePointId = new("different"), FencingToken = 3 },
                new Progress<DatabaseNativeProgress>(), default).AsTask());
    }

    [Fact]
    public async Task Rollback_failure_is_persisted_and_does_not_report_success()
    {
        FailAt("database");
        _platform.RollbackAsync(_request, "preserved-original-data", Arg.Any<CancellationToken>())
            .Returns(_ => ValueTask.FromException(new IOException("rollback failed")));
        await Assert.ThrowsAsync<AggregateException>(() =>
            Coordinator().RecoverAsync(_request, new Progress<DatabaseNativeProgress>(), default).AsTask());
        Assert.Equal(DatabaseReplacementStage.ReconciliationRequired, (await ReadAsync(_request))!.Stage);
    }

    void FailAt(string name)
    {
        ValueTask Failure() { _calls.Add(name); return ValueTask.FromException(new InvalidOperationException("test fault")); }
        switch (name)
        {
            case "stop": _platform.StopApplicationAsync(_request, Arg.Any<CancellationToken>()).Returns(_ => Failure()); break;
            case "replace": _platform.ReplaceDataAsync(_request, "preserved-original-data", Arg.Any<CancellationToken>()).Returns(_ => Failure()); break;
            case "database": _platform.ValidateDatabaseAsync(_request, Arg.Any<CancellationToken>()).Returns(_ => Failure()); break;
            case "start-fenced": _platform.StartApplicationInRecoveryAsync(_request, Arg.Any<CancellationToken>()).Returns(_ => Failure()); break;
            case "application": _platform.ValidateApplicationAsync(_request, Arg.Any<CancellationToken>()).Returns(_ => Failure()); break;
            case "resume": _platform.ResumeAdmissionAsync(_request, Arg.Any<CancellationToken>()).Returns(_ => Failure()); break;
        }
    }

    FileDatabaseReplacementRecoveryJournal Journal() => new(_directory);
    DatabaseDataReplacementRecovery Coordinator() => new(_platform, Journal());
    async Task<DatabaseReplacementRecoveryRecord?> ReadAsync(DatabaseCutoverRequest request)
    {
        await using var lease = await Journal().AcquireAsync(request, default);
        return await lease.ReadAsync(default);
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
