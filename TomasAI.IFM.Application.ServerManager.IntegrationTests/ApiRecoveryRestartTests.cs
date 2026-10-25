using System.Diagnostics;
using System.Text.Json;
using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Application.ServerManager.TestProcess;

namespace TomasAI.IFM.Application.ServerManager.IntegrationTests;

public sealed class ApiRecoveryRestartTests : IDisposable
{
    readonly string root = Path.Combine(Path.GetTempPath(),"ifm-api-restart-"+Guid.NewGuid().ToString("N"));
    string SessionDirectory => Path.Combine(root,"session");
    static ApiRecoveryRestartRequest Request(TimeProvider? clock=null) {
        var now=(clock??TimeProvider.System).GetUtcNow().UtcDateTime;
        return new(Guid.NewGuid(),"test-session",100,now.AddHours(-1),DateOnly.FromDateTime(now),now.AddHours(6),now,["101.701.1901.1501"]);
    }
    ApiRecoveryRestartController Controller(Func<ApiRecoveryRestartRequest,CancellationToken,Task<int>> restart,
        Func<ApiRecoveryRestartRequest,int,CancellationToken,Task>? verify=null,TimeProvider? clock=null,
        Func<ApiRecoveryRestartResult,CancellationToken,Task>? notify=null)
        => new(SessionDirectory,"test-session",restart,verify??((_,_,_)=>Task.CompletedTask),notify??((_,_)=>Task.CompletedTask),_=>{},clock);

    [Fact]
    public async Task Verified_restart_releases_claim_for_a_later_episode_but_duplicate_delivery_is_idempotent()
    {
        int calls=0; var request=Request();
        Task<int> Restart(ApiRecoveryRestartRequest _,CancellationToken token) {calls++;return Task.FromResult(100 + 100 * calls);}
        var first=await Controller(Restart).HandleAsync(request,default);
        Assert.Equal("Recovered",first.State); Assert.Equal(1,calls);
        Assert.Equal(first,await Controller(Restart).HandleAsync(request,default)); Assert.Equal(1,calls);
        var second=await Controller(Restart).HandleAsync(request with{OperationId=Guid.NewGuid(),ProcessId=200},default);
        Assert.Equal("Recovered",second.State);Assert.Equal(2,calls);
        var tomorrow=await Controller(Restart).HandleAsync(request with{OperationId=Guid.NewGuid(),ValueDate=request.ValueDate.AddDays(1)},default);
        Assert.Equal("Recovered",tomorrow.State);Assert.Equal(3,calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Uncooperative_restart_or_proof_cannot_exceed_two_minutes(bool stuckRestart)
    {
        var clock=new ManualTime();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller=Controller((_,_)=>{if(stuckRestart){entered.TrySetResult();return never.Task;}return Task.FromResult(200);},
            (_,_,_)=>{entered.TrySetResult();return never.Task;},clock);
        var request=Request(clock);
        var pending=controller.HandleAsync(request,default);await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(119)); Assert.False(pending.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));var failed=await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Failed",failed.State);Assert.Contains("two-minute",failed.Error);
        var duplicate=await Controller((_,_)=>throw new Exception("must not restart again"),clock:clock)
            .HandleAsync(request with{OperationId=Guid.NewGuid(),RequestedUtc=clock.GetUtcNow().UtcDateTime},default);
        Assert.Contains("outstanding or failed",duplicate.Error);
    }

    [Fact]
    public async Task Claim_remains_until_verification_and_completed_legacy_claim_is_released_after_manager_restart()
    {
        var request = Request();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proof = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = Controller((_, _) => Task.FromResult(200), (_, _, _) => { entered.TrySetResult(); return proof.Task; })
            .HandleAsync(request, default);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var ledger = Path.Combine(root, $"api-restart-{request.ValueDate:yyyy-MM-dd}.json");
        Assert.True(File.Exists(ledger));
        proof.SetResult();
        Assert.Equal("Recovered", (await pending).State);
        Assert.False(File.Exists(ledger));
        // Simulate a manager crash after saving verified success but before deleting its old claim.
        await File.WriteAllTextAsync(ledger, JsonSerializer.Serialize(request));
        var later = await Controller((_, _) => Task.FromResult(300)).HandleAsync(request with {
            OperationId = Guid.NewGuid(), ProcessId = 200 }, default);
        Assert.Equal("Recovered", later.State);
        Assert.False(File.Exists(ledger));
    }

    [Fact]
    public async Task Real_process_restart_and_verified_restore_allow_another_episode_without_touching_other_children()
    {
        ManagedProcessDefinition Definition(string key) => new() { Key = key, DisplayName = key,
            ExecutablePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
            WorkingDirectory = Path.GetDirectoryName(typeof(TestProcessMarker).Assembly.Location)!,
            Arguments = [typeof(TestProcessMarker).Assembly.Location, "--delay-ms", "60000"] };
        await using var supervisor = new ManagedProcessSupervisor([Definition("api"), Definition("market-data"), Definition("ui")],
            TimeSpan.FromSeconds(10), _ => { });
        await supervisor.StartAllAsync();
        var unchanged = supervisor.RunningProcesses.Where(process => process.ProcessKey != "api").ToArray();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var old = supervisor.RunningProcesses.Single(process => process.ProcessKey == "api");
            var request = Request() with { ProcessId = old.ProcessId, ProcessStartedUtc = old.StartedAtUtc.UtcDateTime };
            if (attempt == 1)
            {
                // A crash or deployment may remove the process before escalation reads it.
                using var exited = Process.GetProcessById(old.ProcessId);
                exited.Kill();
                await exited.WaitForExitAsync();
                await supervisor.WaitForExitAsync("api").WaitAsync(TimeSpan.FromSeconds(5));
            }
            var restartCalls = 0; var proofCalls = 0;
            var result = await Controller((value, token) => Interlocked.Increment(ref restartCalls) <= 4
                    ? Task.FromException<int>(new IOException("Injected transient API launch failure"))
                    : supervisor.RestartApiAsync(value.ProcessId, value.ProcessStartedUtc, token),
                (_, pid, _) => { if (Interlocked.Increment(ref proofCalls) <= 4) throw new InvalidOperationException("Injected restore/readiness failure");
                    using var process = Process.GetProcessById(pid); Assert.False(process.HasExited); return Task.CompletedTask; })
                .HandleAsync(request, default);
            Assert.Equal("Recovered", result.State);
            Assert.Equal(5, restartCalls); Assert.Equal(5, proofCalls);
            Assert.NotEqual(old.ProcessId, result.ReplacementProcessId);
            foreach (var child in unchanged) Assert.Equal(child, supervisor.RunningProcesses.Single(process => process.ProcessKey == child.ProcessKey));
        }
    }

    [Fact]
    public async Task Early_error_waits_for_initial_recovery_window_and_expired_sessions_never_kill_anything()
    {
        var clock=new ManualTime();int calls=0;var request=Request(clock) with{RecoveryDeadlineUtc=clock.GetUtcNow().UtcDateTime.AddMinutes(2)};
        var controller=Controller((_,_)=>{calls++;return Task.FromResult(200);},clock:clock);
        var pending=controller.HandleAsync(request,default);clock.Advance(TimeSpan.FromSeconds(119));Assert.Equal(0,calls);
        clock.Advance(TimeSpan.FromSeconds(1));Assert.Equal("Recovered",(await pending.WaitAsync(TimeSpan.FromSeconds(5))).State);
        var expired=await controller.HandleAsync(request with{OperationId=Guid.NewGuid(),SessionEndUtc=clock.GetUtcNow().UtcDateTime.AddSeconds(-1)},default);
        Assert.Equal("Rejected",expired.State);Assert.Equal(1,calls);
    }

    [Fact]
    public async Task Failed_restart_notifies_from_external_controller_even_when_API_never_starts_and_delivery_retries()
    {
        var clock = new ManualTime();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Directory.CreateDirectory(SessionDirectory);var request=Request(clock);
        await File.WriteAllTextAsync(ApiRecoveryRestartProtocol.RequestPath(SessionDirectory,request.OperationId),JsonSerializer.Serialize(request));
        int deliveries=0;var seen=new TaskCompletionSource<ApiRecoveryRestartResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var controller=Controller((_,_)=>{ entered.TrySetResult(); throw new IOException("API launch failed"); },clock:clock,notify:(result,_)=>{
            if(Interlocked.Increment(ref deliveries)==1)throw new IOException("broker unavailable");
            seen.TrySetResult(result);return Task.CompletedTask;
        });
        using var stop=new CancellationTokenSource();var loop=controller.RunAsync(stop.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)); clock.Advance(TimeSpan.FromMinutes(2));
        try{Assert.Contains("two-minute API restart/restore deadline expired",(await seen.Task.WaitAsync(TimeSpan.FromSeconds(8))).Error);Assert.Equal(2,deliveries);}
        finally{await stop.CancelAsync();await loop;}
    }

    [Fact]
    public async Task Real_API_process_is_force_replaced_without_stopping_host_futures_or_UI_and_stale_identity_is_rejected()
    {
        ManagedProcessDefinition Definition(string key)=>new(){Key=key,DisplayName=key,
            ExecutablePath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"dotnet","dotnet.exe"),
            WorkingDirectory=Path.GetDirectoryName(typeof(TestProcessMarker).Assembly.Location)!,
            Arguments=[typeof(TestProcessMarker).Assembly.Location,"--delay-ms","60000"]};
        await using var supervisor=new ManagedProcessSupervisor([Definition("api"),Definition("market-data"),Definition("ui")],TimeSpan.FromSeconds(10),_=>{});
        await supervisor.StartAllAsync();var before=supervisor.RunningProcesses.ToDictionary(p=>p.ProcessKey);
        var api=before["api"];
        await Assert.ThrowsAsync<InvalidOperationException>(()=>supervisor.RestartApiAsync(api.ProcessId,api.StartedAtUtc.UtcDateTime.AddSeconds(-1),default));
        Assert.Equal(api.ProcessId,supervisor.RunningProcesses.Single(p=>p.ProcessKey=="api").ProcessId);
        using var budget=new CancellationTokenSource(TimeSpan.FromSeconds(10));var timer=Stopwatch.StartNew();
        var replacement=await supervisor.RestartApiAsync(api.ProcessId,api.StartedAtUtc.UtcDateTime,budget.Token);
        Assert.NotEqual(api.ProcessId,replacement);Assert.True(timer.Elapsed<TimeSpan.FromSeconds(8));
        foreach(var key in new[]{"market-data","ui"})Assert.Equal(before[key],supervisor.RunningProcesses.Single(p=>p.ProcessKey==key));
        // An API crash between handoff and manager observation must still allow relaunch of the known identity.
        var crashed=supervisor.RunningProcesses.Single(p=>p.ProcessKey=="api");
        using(var process=Process.GetProcessById(crashed.ProcessId)){process.Kill(false);await process.WaitForExitAsync(budget.Token);}
        await supervisor.WaitForExitAsync("api",budget.Token);
        var relaunched=await supervisor.RestartApiAsync(crashed.ProcessId,crashed.StartedAtUtc.UtcDateTime,budget.Token);
        Assert.NotEqual(crashed.ProcessId,relaunched);
    }

    public void Dispose(){if(Directory.Exists(root)&&root.StartsWith(Path.GetTempPath(),StringComparison.OrdinalIgnoreCase)
        &&Path.GetFileName(root).StartsWith("ifm-api-restart-",StringComparison.Ordinal))Directory.Delete(root,true);}

    sealed class ManualTime : TimeProvider
    {
        DateTimeOffset now=DateTimeOffset.UtcNow;readonly List<ManualTimer> timers=[];
        public override DateTimeOffset GetUtcNow()=>now;
        public void Advance(TimeSpan amount){now+=amount;ManualTimer[] copy;lock(timers)copy=timers.ToArray();foreach(var timer in copy)timer.Fire(now);}
        public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period){var timer=new ManualTimer(this,callback,state);timer.Change(dueTime,period);lock(timers)timers.Add(timer);return timer;}
        sealed class ManualTimer(ManualTime clock,TimerCallback callback,object? state):ITimer
        {
            DateTimeOffset due;TimeSpan period;bool disposed;
            public bool Change(TimeSpan delay,TimeSpan interval){due=delay==Timeout.InfiniteTimeSpan?DateTimeOffset.MaxValue:clock.now+delay;period=interval;return !disposed;}
            public void Fire(DateTimeOffset now){if(disposed||now<due)return;due=period>TimeSpan.Zero?now+period:DateTimeOffset.MaxValue;callback(state);}
            public void Dispose()=>disposed=true;public ValueTask DisposeAsync(){Dispose();return ValueTask.CompletedTask;}
        }
    }
}
