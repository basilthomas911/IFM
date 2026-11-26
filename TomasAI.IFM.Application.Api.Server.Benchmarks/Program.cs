using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

if (args.Length == 2 && args[0] == "--export-original-raw-partitions")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.OriginalRawPartitionExport.RunAsync(args[1]);
    return;
}

if (args.Length == 7 && args[0] == "--export-recorded-monitoring-authority")
{
    await TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes.ReplayMonitoringAuthority.ExportAsync(args[1], args[2], args[3], args[4], args[5], args[6]);
    return;
}

if (args.Length == 2 && args[0] == "--replay-original-session")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.OriginalSessionReplay.RunAsync(args[1]);
    return;
}

if (args.Length == 3 && args[0] == "--qualify-realtime-cold")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.RealtimeLoadQualification.RunColdAsync(args[1], args[2]);
    return;
}

if (args.Length == 3 && args[0] == "--qualify-realtime-load")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.RealtimeLoadQualification.RunAsync(args[1], args[2]);
    return;
}

if (args.Contains("--verify-workflow-boundary-coverage", StringComparer.Ordinal))
{
    TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowBoundaryCoverageBenchmarks.VerifyAll();
    return;
}
if (args.Contains("--workflow-boundary-coverage-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("WorkflowBoundaryCoverage")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(2).WithIterationCount(5).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowBoundaryCoverageBenchmarks>(configuration);
    return;
}
if (args.Length == 5 && args[0] == "--profile-durable-mailbox")
{
    var mode = args[1]; var payloadBytes = int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
    var startFile = args[3]; var outputFile = args[4];
    var probe = new TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.DurableMailboxOwnershipBenchmarks { Count = 64, PayloadBytes = payloadBytes, HandlerBatching = true };
    await probe.Setup();
    try
    {
        Func<Task> run = mode switch
        {
            "Core" => probe.BeforeCorePubSub,
            "SerialMemory" => probe.AfterSerialMemoryDurableOwnership,
            "BatchMemory" => probe.AfterMemoryDurableOwnership,
            "BatchFile" => probe.AfterFileDurableOwnership,
            _ => throw new ArgumentException("Unknown mailbox profile mode.")
        };
        await run(); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Console.WriteLine($"PROFILE_READY pid={Environment.ProcessId} mode={mode}");
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        while (!File.Exists(startFile)) await Task.Delay(50, timeout.Token);
        TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.MailboxQualificationTrace.Log.WorkloadStart(mode);
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var cpuBefore = process.TotalProcessorTime; var pauseBefore = GC.GetTotalPauseDuration();
        var allocated = GC.GetTotalAllocatedBytes(true); var gen2 = GC.CollectionCount(2); var durations = new List<double>();
        for (var iteration = 0; iteration < 8; iteration++)
        { var watch = System.Diagnostics.Stopwatch.StartNew(); await run(); durations.Add(watch.Elapsed.TotalMilliseconds); }
        var allocatedBytes = GC.GetTotalAllocatedBytes(true) - allocated; var gen2Collections = GC.CollectionCount(2) - gen2;
        TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.MailboxQualificationTrace.Log.WorkloadStop(mode);
        process.Refresh();
        await File.WriteAllTextAsync(outputFile, System.Text.Json.JsonSerializer.Serialize(new { Mode = mode, PayloadBytes = payloadBytes,
            Sources = 512, AllocatedBytes = allocatedBytes, Gen2Collections = gen2Collections, Batch64Milliseconds = durations,
            CpuMilliseconds = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds,
            GcPauseMilliseconds = (GC.GetTotalPauseDuration() - pauseBefore).TotalMilliseconds,
            EndingWorkingSetBytes = process.WorkingSet64, PeakProcessWorkingSetBytes = process.PeakWorkingSet64,
            Note = "CPU, allocations and GC deltas cover the marked 512-source workload. Peak process working set includes setup and trace attachment; it is not a workload-only RAM delta."
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PROFILE_COMPLETE {outputFile}");
    }
    finally { await probe.Cleanup(); }
    return;
}
if (args.Contains("--iti-ordered-source-batch-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("ItiOrderedSourceBatch")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(2).WithIterationCount(5).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.ItiOrderedSourceBatchBenchmarks>(configuration);
    return;
}
if (args.Contains("--measure-durable-mailbox-ownership", StringComparer.Ordinal))
{
    var probe = new TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.DurableMailboxOwnershipBenchmarks { Count = 64, PayloadBytes = 512, HandlerBatching = true };
    await probe.Setup();
    try
    {
        foreach (var item in new (string Name, Func<Task> Run)[] { ("Core", probe.BeforeCorePubSub),
            ("SerialMemory", probe.AfterSerialMemoryDurableOwnership), ("BatchMemory", probe.AfterMemoryDurableOwnership),
            ("SerialFile", probe.AfterSerialFileDurableOwnership), ("BatchFile", probe.AfterFileDurableOwnership) })
        {
            var watch = System.Diagnostics.Stopwatch.StartNew(); await item.Run(); watch.Stop();
            Console.WriteLine($"{item.Name}: count=64 payload=512 elapsedMs={watch.Elapsed.TotalMilliseconds:F3} sourcesPerSecond={64 / watch.Elapsed.TotalSeconds:F1}");
        }
    }
    finally { await probe.Cleanup(); }
    return;
}
if (args.Contains("--dataset-source-admission-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("DatasetSourceAdmission")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(4096).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.DatasetSourceAdmissionBenchmarks>(configuration);
    return;
}
if (args.Contains("--durable-mailbox-ownership-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("DurableMailboxOwnership")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(2).WithIterationCount(5).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.DurableMailboxOwnershipBenchmarks>(configuration);
    return;
}
if (args.Contains("--workflow-publication-replay-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("WorkflowPublicationReplay")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(2).WithIterationCount(5).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowPublicationReplayBenchmarks>(configuration);
    return;
}
if (args.Contains("--destination-plan-replay-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("DestinationPlanReplay")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(2).WithIterationCount(5).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.DestinationPlanReplayBenchmarks>(configuration);
    return;
}
if (args.Length==3 && args[0]=="--workflow-regime-completion-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowRegimeCompletionVerification.CrashChild(args[1],args[2]);return;
}
if (args.Contains("--verify-workflow-regime-completion",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowRegimeCompletionVerification.Verify();return;
}
if (args.Contains("--workflow-regime-completion-benchmarks",StringComparer.Ordinal))
{
    var representative=MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events.WorkflowStrategyStateUpdatedEvent>(
        File.ReadAllBytes("C:/repos/IFM/.artifacts/realtime-mailboxes/20261125-safety/workflow-regime-completion-real-source.msgpack"));
    var sizes=new{RepresentativeRequestBytes=MessagePack.MessagePackSerializer.Serialize(representative).Length,
        SmallRequestBytes=MessagePack.MessagePackSerializer.Serialize(TomasAI.IFM.TestSupport.WorkflowDispatchDeliveryFixture.RegimeFunctionInput()).Length};
    File.WriteAllText("C:/repos/IFM/.artifacts/realtime-mailboxes/20261125-safety/workflow-regime-completion-request-sizes.json",System.Text.Json.JsonSerializer.Serialize(sizes));
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("WorkflowRegimeCompletion")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(5).WithIterationCount(8).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowRegimeCompletionBenchmarks>(configuration);return;
}

if (args.Length==3 && args[0]=="--workflow-regime-function-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowRegimeFunctionVerification.CrashChild(args[1],args[2]);return;
}
if (args.Contains("--verify-workflow-regime-function",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowRegimeFunctionVerification.Verify();return;
}
if (args.Contains("--workflow-regime-function-benchmarks",StringComparer.Ordinal))
{
    var representative=MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events.WorkflowStrategyStateUpdatedEvent>(
        File.ReadAllBytes("C:/repos/IFM/.artifacts/realtime-mailboxes/20261125-safety/workflow-regime-function-real-source.msgpack"));
    var sizes=new{RepresentativeRequestBytes=MessagePack.MessagePackSerializer.Serialize(representative).Length,
        SmallRequestBytes=MessagePack.MessagePackSerializer.Serialize(TomasAI.IFM.TestSupport.WorkflowDispatchDeliveryFixture.RegimeFunctionInput()).Length};
    File.WriteAllText("C:/repos/IFM/.artifacts/realtime-mailboxes/20261125-safety/workflow-regime-function-request-sizes.json",System.Text.Json.JsonSerializer.Serialize(sizes));
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("WorkflowRegimeFunction")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(5).WithIterationCount(8).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowRegimeFunctionBenchmarks>(configuration);return;
}

if (args.Length==3 && args[0]=="--workflow-assessment-function-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowAssessmentFunctionVerification.CrashChild(args[1],args[2]);return;
}
if (args.Contains("--verify-workflow-assessment-function",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowAssessmentFunctionVerification.Verify();return;
}
if (args.Contains("--workflow-assessment-function-benchmarks",StringComparer.Ordinal))
{
    var representative=MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events.WorkflowStrategyStateUpdatedEvent>(
        File.ReadAllBytes("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/workflow-assessment-function-real-source.msgpack"));
    var sizes=new{RepresentativeRequestBytes=MessagePack.MessagePackSerializer.Serialize(representative).Length,
        SmallRequestBytes=MessagePack.MessagePackSerializer.Serialize(TomasAI.IFM.TestSupport.WorkflowDispatchDeliveryFixture.AssessmentFunctionInput()).Length};
    File.WriteAllText("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/workflow-assessment-function-request-sizes.json",System.Text.Json.JsonSerializer.Serialize(sizes));
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("WorkflowAssessmentFunction")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(5).WithIterationCount(8).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowAssessmentFunctionBenchmarks>(configuration);return;
}

if (args.Length==3 && args[0]=="--workflow-selection-function-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowSelectionFunctionVerification.CrashChild(args[1],args[2]);return;
}
if (args.Contains("--verify-workflow-selection-function",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowSelectionFunctionVerification.Verify();return;
}
if (args.Contains("--workflow-selection-function-benchmarks",StringComparer.Ordinal))
{
    var representative=MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events.WorkflowStrategyStateUpdatedEvent>(
        File.ReadAllBytes("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/workflow-selection-function-real-source.msgpack"));
    var sizes=new{RepresentativeRequestBytes=MessagePack.MessagePackSerializer.Serialize(representative.State.SelectionDispatch!).Length,
        SmallRequestBytes=MessagePack.MessagePackSerializer.Serialize(TomasAI.IFM.TestSupport.WorkflowDispatchDeliveryFixture.SelectionFunctionInput().State.SelectionDispatch!).Length};
    File.WriteAllText("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/workflow-selection-function-request-sizes.json",System.Text.Json.JsonSerializer.Serialize(sizes));
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("WorkflowSelectionFunction")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(5).WithIterationCount(8).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowSelectionFunctionBenchmarks>(configuration);return;
}

if (args.Length==3 && args[0]=="--workflow-composition-function-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowCompositionFunctionVerification.CrashChild(args[1],args[2]);return;
}
if (args.Contains("--verify-workflow-composition-function",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowCompositionFunctionVerification.Verify();return;
}
if (args.Contains("--workflow-composition-function-benchmarks",StringComparer.Ordinal))
{
    var representative=MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events.WorkflowStrategyStateUpdatedEvent>(
        File.ReadAllBytes("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/workflow-composition-function-real-source.msgpack"));
    var sizes=new{RepresentativeRequestBytes=MessagePack.MessagePackSerializer.Serialize(representative.State.CompositionExecution!).Length,
        SmallRequestBytes=MessagePack.MessagePackSerializer.Serialize(TomasAI.IFM.TestSupport.WorkflowDispatchDeliveryFixture.CompositionFunctionInput().State.CompositionExecution!).Length};
    File.WriteAllText("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/workflow-composition-function-request-sizes.json",System.Text.Json.JsonSerializer.Serialize(sizes));
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("WorkflowCompositionFunction")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(5).WithIterationCount(8).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowCompositionFunctionBenchmarks>(configuration);return;
}

if (args.Length==3 && args[0]=="--workflow-reservation-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowReservationVerification.CrashChild(args[1],args[2]);return;
}
if (args.Contains("--verify-workflow-reservation",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowReservationVerification.Verify();return;
}
if (args.Contains("--workflow-reservation-benchmarks",StringComparer.Ordinal))
{
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("WorkflowReservation")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(5).WithIterationCount(8).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowReservationBenchmarks>(configuration);return;
}

if (args.Length==3 && args[0]=="--workflow-market-condition-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowMarketConditionVerification.CrashChild(args[1],args[2]);return;
}
if (args.Contains("--verify-workflow-market-condition",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowMarketConditionVerification.Verify();return;
}
if (args.Contains("--workflow-market-condition-benchmarks",StringComparer.Ordinal))
{
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("WorkflowMarketCondition")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(5).WithIterationCount(8).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowMarketConditionBenchmarks>(configuration);return;
}

if (args.Length==3 && args[0]=="--workflow-composition-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowCompositionVerification.CrashChild(args[1],args[2]);return;
}
if (args.Contains("--verify-workflow-composition",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowCompositionVerification.Verify();return;
}
if (args.Contains("--workflow-composition-benchmarks",StringComparer.Ordinal))
{
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("WorkflowComposition")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(5).WithIterationCount(8).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowCompositionBenchmarks>(configuration);return;
}

if (args.Length==3 && args[0]=="--workflow-initialization-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowInitializationVerification.CrashChild(args[1],args[2]);return;
}
if (args.Contains("--verify-workflow-initialization",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowInitializationVerification.Verify();return;
}

if (args.Contains("--workflow-initialization-benchmarks",StringComparer.Ordinal))
{
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("WorkflowInitialization")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(5).WithIterationCount(8).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowInitializationBenchmarks>(configuration);return;
}

if (args.Length==3 && args[0]=="--workflow-dispatch-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowDispatchVerification.CrashChild(args[1],args[2]);return;
}
if (args.Contains("--verify-workflow-dispatch",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowDispatchVerification.Verify();return;
}

if (args.Contains("--workflow-dispatch-benchmarks",StringComparer.Ordinal))
{
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("WorkflowDispatchSettlement")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(5).WithIterationCount(8).WithInvocationCount(2048).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.WorkflowDispatchBenchmarks>(configuration);return;
}

if (args.Length==5 && args[0]=="--broker-observation-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.BrokerObservationVerification.CrashChild(args[1],int.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture),args[3],args[4]);return;
}
if (args.Contains("--verify-broker-observations",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.BrokerObservationVerification.Verify();return;
}

if (args.Contains("--broker-observation-benchmarks",StringComparer.Ordinal))
{
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("BrokerObservationPublication")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(5).WithIterationCount(8).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.BrokerObservationBenchmarks>(configuration);return;
}

if (args.Length == 5 && args[0] == "--emulator-checkpoint-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.EmulatorCheckpointVerification.CrashChild(args[1],int.Parse(args[2],System.Globalization.CultureInfo.InvariantCulture),args[3],args[4]);return;
}
if (args.Contains("--verify-emulator-checkpoint",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.EmulatorCheckpointVerification.Verify();return;
}
if (args.Contains("--emulator-checkpoint-benchmarks",StringComparer.Ordinal))
{
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("EmulatorCheckpoint")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(5).WithIterationCount(8).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.EmulatorCheckpointBenchmarks>(configuration);return;
}
if (args.Length == 4 && args[0] == "--broker-order-settlement-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.BrokerOrderSettlementVerification.CrashChild(args[1],args[2],args[3]);return;
}
if (args.Contains("--verify-broker-order-settlement",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.BrokerOrderSettlementVerification.Verify();return;
}
if (args.Contains("--broker-order-settlement-benchmarks",StringComparer.Ordinal))
{
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("BrokerOrderSettlement")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(10).WithIterationCount(8).WithInvocationCount(4096).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.BrokerOrderSettlementBenchmarks>(configuration);return;
}
if (args.Length == 4 && args[0] == "--option-routing-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.FuturesOptionRoutingVerification.CrashChild(args[1],args[2],args[3]);return;
}
if (args.Contains("--verify-option-routing",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.FuturesOptionRoutingVerification.Verify();return;
}
if (args.Contains("--option-routing-benchmarks", StringComparer.Ordinal))
{
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("FuturesOptionRouting")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(10).WithIterationCount(8).WithInvocationCount(64).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.FuturesOptionRoutingBenchmarks>(configuration);return;
}
if (args.Length == 4 && args[0] == "--futures-routing-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.FuturesRoutingVerification.CrashChild(args[1],args[2],args[3]);return;
}
if (args.Contains("--verify-futures-routing",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.FuturesRoutingVerification.Verify();return;
}
if (args.Contains("--futures-routing-benchmarks", StringComparer.Ordinal))
{
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("FuturesRouting")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(10).WithIterationCount(8).WithInvocationCount(64).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.FuturesRoutingBenchmarks>(configuration);return;
}
if (args.Length == 4 && args[0] == "--daily-exit-intent-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.DailyFuturesExitIntentVerification.CrashChild(args[1],args[2],args[3]); return;
}
if (args.Contains("--verify-daily-exit-intents",StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.DailyFuturesExitIntentVerification.Verify(); return;
}
if (args.Contains("--daily-exit-intent-benchmarks",StringComparer.Ordinal))
{
    var configuration=DefaultConfig.Instance.AddJob(Job.Default.WithId("DailyFuturesExitIntent")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(10).WithIterationCount(8).WithInvocationCount(64).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.DailyFuturesExitIntentBenchmarks>(configuration);return;
}
if (args.Length == 4 && args[0] == "--futures-settlement-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.FuturesPositionSettlementVerification.CrashChild(args[1], args[2], args[3]); return;
}
if (args.Contains("--verify-futures-position-settlement", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.FuturesPositionSettlementVerification.Verify(); return;
}
if (args.Contains("--futures-position-settlement-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("FuturesPositionSettlement")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(10).WithIterationCount(8).WithInvocationCount(4096).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.FuturesPositionSettlementBenchmarks>(configuration);
    return;
}
if (args.Length == 4 && args[0] == "--vertical-settlement-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.VerticalSpreadPositionSettlementVerification.CrashChild(args[1], args[2], args[3]); return;
}
if (args.Contains("--verify-vertical-position-settlement", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.VerticalSpreadPositionSettlementVerification.Verify(); return;
}
if (args.Contains("--vertical-position-settlement-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("VerticalSpreadPositionSettlement")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(10).WithIterationCount(8).WithInvocationCount(4096).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.VerticalSpreadPositionSettlementBenchmarks>(configuration);
    return;
}
if (args.Length == 4 && args[0] == "--condor-publication-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.IronCondorPlanPublicationVerification.CrashChild(args[1], args[2], args[3]); return;
}
if (args.Contains("--verify-iron-condor-plan-publication", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.IronCondorPlanPublicationVerification.Verify(); return;
}
if (args.Contains("--iron-condor-plan-publication-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("IronCondorPlanPublication")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(10).WithIterationCount(8).WithInvocationCount(64).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.IronCondorPlanPublicationBenchmarks>(configuration);
    return;
}
if (args.Length == 4 && args[0] == "--condor-replay-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.IronCondorPositionReplayVerification.CrashChild(args[1], args[2], args[3]); return;
}
if (args.Contains("--verify-iron-condor-position-replay", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.IronCondorPositionReplayVerification.Verify(); return;
}
if (args.Contains("--iron-condor-position-replay-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("IronCondorPositionReplay")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(10).WithIterationCount(8).WithInvocationCount(64).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.IronCondorPositionReplayBenchmarks>(configuration);
    return;
}
if (args.Length == 4 && args[0] == "--condor-settlement-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.IronCondorPositionSettlementVerification.CrashChild(args[1], args[2], args[3]); return;
}
if (args.Contains("--verify-iron-condor-position-settlement", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.IronCondorPositionSettlementVerification.Verify(); return;
}
if (args.Contains("--iron-condor-position-settlement-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("IronCondorPositionSettlement")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(10).WithIterationCount(8).WithInvocationCount(4096).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.IronCondorPositionSettlementBenchmarks>(configuration);
    return;
}
if (args.Contains("--audit-position-delivery", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.PositionDeliveryAudit.Verify(); return;
}
if (args.Length == 4 && args[0] == "--tick-settlement-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.TickStorageSettlementVerification.CrashChild(args[1], args[2], args[3]); return;
}
if (args.Contains("--verify-tick-storage-settlement", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.TickStorageSettlementVerification.Verify(); return;
}
if (args.Contains("--tick-storage-settlement-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("TickStorageSettlement")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(5).WithIterationCount(8).WithInvocationCount(4096).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.TickStorageSettlementBenchmarks>(configuration);
    return;
}
if (args.Length == 4 && args[0] == "--option-settlement-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.OptionTradeSettlementVerification.CrashChild(args[1], bool.Parse(args[2]), args[3]); return;
}
if (args.Contains("--verify-option-trade-settlement", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.OptionTradeSettlementVerification.Verify(); return;
}
if (args.Contains("--option-trade-settlement-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("OptionTradeSettlement")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(5).WithIterationCount(8).WithInvocationCount(4096).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.OptionTradeSettlementBenchmarks>(configuration);
    return;
}
if (args.Length == 4 && args[0] == "--buffered-eod-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.BufferedEodVerification.CrashChild(args[1], args[2], args[3]); return;
}
if (args.Contains("--verify-buffered-eod", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.BufferedEodVerification.Verify(); return;
}
if (args.Contains("--buffered-eod-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("BufferedEod")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(3).WithIterationCount(10).WithInvocationCount(1).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkRunner.Run<TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.BufferedEodBenchmarks>(configuration);
    return;
}
if (args.Length == 4 && args[0] == "--eod-settlement-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.EodSettlementVerification.CrashChild(args[1], args[2], bool.Parse(args[3]));
    return;
}
if (args.Contains("--verify-eod-settlement", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.EodSettlementVerification.Verify();
    return;
}
if (args.Contains("--eod-settlement-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("EodSettlement")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(5).WithIterationCount(8).WithInvocationCount(8192).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkSwitcher.FromTypes([
        typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.EodWorkerSettlementBenchmarks),
        typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.EodInlineSettlementBenchmarks)])
        .Run(args.Where(arg => arg != "--eod-settlement-benchmarks").ToArray(), configuration);
    return;
}

if (args.Length == 4 && args[0] == "--iti-settlement-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.ItiSettlementVerification.CrashChild(args[1], args[2], args[3]);
    return;
}
if (args.Contains("--verify-iti-settlement", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.ItiSettlementVerification.Verify();
    return;
}
if (args.Contains("--iti-settlement-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("ItiSettlement")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(8).WithIterationCount(8).WithInvocationCount(64).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkSwitcher.FromTypes([typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.ItiSettlementBenchmarks)])
        .Run(args.Where(arg => arg != "--iti-settlement-benchmarks").ToArray(), configuration);
    return;
}

if (args.Length == 4 && args[0] == "--indicator-settlement-crash-child")
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.IndicatorSettlementVerification.CrashChild(args[1], args[2], args[3]);
    return;
}
if (args.Contains("--indicator-settlement-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("IndicatorSettlement")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(8).WithIterationCount(8).WithInvocationCount(4096).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkSwitcher.FromTypes([typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.IndicatorSettlementBenchmarks)])
        .Run(args.Where(arg => arg != "--indicator-settlement-benchmarks").ToArray(), configuration);
    return;
}
if (args.Contains("--verify-indicator-settlement", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.IndicatorSettlementVerification.Verify();
    return;
}

if (args.Length == 3 && args[0] == "--tdi-receipt-crash-child")
{
    var receipts = new TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.FuturesTdiResultReceipts(args[1]);
    var scenario = new TomasAI.IFM.TestSupport.TdiReceiptFixture.Scenario(receipts)
    {
        ThrowAfterSource = args[2] == "source", ThrowAfterDatabase = args[2] == "database", ThrowAfterCompletion = args[2] == "completion"
    };
    await scenario.Start();
    try { await scenario.Process(TomasAI.IFM.TestSupport.TdiReceiptFixture.Source()); }
    catch (IOException) { }
    Environment.Exit(0); // Deliberately bypass receipt and actor disposal after an ambiguous durable boundary.
}
if (args.Contains("--verify-tdi-receipts", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.TdiReceiptVerification.VerifyPostgres();
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.TdiReceiptVerification.VerifyCrash();
    return;
}
if (args.Contains("--tdi-receipt-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("TdiReceipt")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(8).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkSwitcher.FromTypes([typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.TdiReceiptBenchmarks)])
        .Run(args.Where(arg => arg != "--tdi-receipt-benchmarks").ToArray(), configuration);
    return;
}

if (args.Length == 3 && args[0] == "--bar-recovery-crash-child")
{
    var store = new TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Model.FuturesTradeSessionBarCheckpointStore(args[1]);
    var handler = new TomasAI.IFM.TestSupport.RealtimeMailboxSafetyFixture.Handler(store);
    await handler.Receive(TomasAI.IFM.TestSupport.RealtimeMailboxSafetyFixture.Trade(1));
    if (args[2] == "pending")
    {
        handler.Fail = true;
        try { await handler.Receive(TomasAI.IFM.TestSupport.RealtimeMailboxSafetyFixture.Trade(2)); }
        catch (IOException) { }
    }
    if (args[2].StartsWith("batch-", StringComparison.Ordinal))
    {
        handler.Fail = args[2] == "batch-pending";
        var inputs = Enumerable.Range(1, 64).Select(n => (TomasAI.IFM.Shared.EventSourcing.IEvent)TomasAI.IFM.TestSupport.RealtimeMailboxSafetyFixture.Trade(n)).ToArray();
        try { await handler.ReceiveBatch(inputs); }
        catch (IOException) when (handler.Fail) { }
    }
    Environment.Exit(0); // Bypass actor shutdown and store disposal after the durable input boundary.
}
if (args.Contains("--verify-bar-recovery-crash", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.BarRecoveryVerification.VerifyCrash();
    return;
}

if (args.Contains("--bar-scheduler-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("BarScheduler")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(8).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkSwitcher.FromTypes([typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.BarSchedulerBenchmarks)])
        .Run(args.Where(arg => arg != "--bar-scheduler-benchmarks").ToArray(), configuration);
    return;
}
if (args.Contains("--verify-bar-scheduler-benchmarks", StringComparer.Ordinal))
{
    var test = new TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.BarSchedulerBenchmarks { Count = 64 };
    try { await test.Setup(); await test.PerMessageBefore(); await test.BatchedAfter(); Console.WriteLine("Real scheduler: both 64-input paths completed every delivery and disposal once."); }
    finally { await test.Cleanup(); }
    return;
}

if (args.Contains("--bar-checkpoint-benchmarks", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("BarCheckpoint")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(16).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkSwitcher.FromTypes([typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.BarCheckpointBenchmarks)])
        .Run(args.Where(arg => arg != "--bar-checkpoint-benchmarks").ToArray(), configuration);
    return;
}

if (args.Contains("--realtime-mailbox-transport-probe", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("RealtimeMailboxTransportProbe")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(8).WithUnrollFactor(1).WithGcForce(false));
    try
    {
        await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.RealtimeMailboxTransportProbe.StartBroker();
        BenchmarkSwitcher.FromTypes([typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.RealtimeMailboxTransportProbe)])
            .Run(args.Where(arg => arg != "--realtime-mailbox-transport-probe").ToArray(), configuration);
    }
    finally { await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.RealtimeMailboxTransportProbe.StopBroker(); }
    return;
}
if (args.Contains("--verify-realtime-mailbox-transport-probe", StringComparer.Ordinal))
{
    var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.RealtimeMailboxTransportProbe { Count = 64, PayloadBytes = 8192 };
    try { await fixture.Setup(); Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await fixture.Verify())); }
    finally { await fixture.Cleanup(); await TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes.RealtimeMailboxTransportProbe.StopBroker(); }
    return;
}

if (args.Contains("--verify-realtime-mailbox-safety", StringComparer.Ordinal))
{
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await TomasAI.IFM.TestSupport.RealtimeMailboxSafetyFixture.Verify(),
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return;
}

if (args.Length == 2 && args[0] == "--wave-wal-crash-child")
{
    var wal = new TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.FuturesItiInputWal(args[1]);
    await Task.WhenAll(Enumerable.Range(0, 64).Select(n => wal.AppendAsync(BitConverter.GetBytes(n), default).AsTask()));
    Environment.Exit(0); // Deliberately bypass DisposeAsync after durable acknowledgements.
}
if (args.Contains("--verify-wave-wal-crash", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave.PerformanceWaveVerification.VerifyAbruptWalExit();
    return;
}

if (args.Contains("--verify-performance-wave", StringComparer.Ordinal))
{
    TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave.PerformanceWaveVerification.Verify();
    return;
}

if (args.Contains("--verify-tradeplan-wave", StringComparer.Ordinal))
{
    await using var client = new TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave.PerformanceWaveTradePlanClient();
    await client.Verify(); return;
}
if (args.Contains("--tradeplan-live-wave", StringComparer.Ordinal))
{
    var configuration = DefaultConfig.Instance.AddJob(Job.Default.WithId("TradePlanLiveWave")
        .WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(8).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkSwitcher.FromTypes([typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave.TradePlanPageWaveBenchmarks)])
        .Run(args.Where(arg => arg != "--tradeplan-live-wave").ToArray(), configuration);
    return;
}

if (args.Contains("--performance-wave", StringComparer.Ordinal))
{
    var configuration = BenchmarkDotNet.Configs.DefaultConfig.Instance.AddJob(BenchmarkDotNet.Jobs.Job.Default
        .WithId("ApiPerformanceWave").WithToolchain(BenchmarkDotNet.Toolchains.InProcess.NoEmit.InProcessNoEmitToolchain.Instance)
        .WithWarmupCount(3).WithIterationCount(8).WithInvocationCount(16).WithUnrollFactor(1).WithGcForce(false));
    BenchmarkSwitcher.FromTypes([
        typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave.OriginWaveBenchmarks),
        typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave.WalWaveBenchmarks),
        typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave.PositionWaveBenchmarks),
        typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave.PostgresPayloadWaveBenchmarks),
        typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave.MetricWaveBenchmarks),
        typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave.EodSpoolWaveBenchmarks)
    ]).Run(args.Where(arg => arg != "--performance-wave").ToArray(), configuration);
    return;
}

if (args.Contains("--verify-iti-causal-continuation", StringComparer.Ordinal))
{
    var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.FuturesItiStorageBaselineBenchmarks { Mode = "Fast" };
    try { await fixture.Setup(); await fixture.VerifyCausalContinuationImportAsync(args[^1]); }
    finally { await fixture.Cleanup(); }
    return;
}

if (args.Contains("--verify-iti-causal-import", StringComparer.Ordinal))
{
    var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.FuturesItiStorageBaselineBenchmarks { Mode = "Fast" };
    try { await fixture.Setup(); await fixture.VerifyCausalPrefixImportAsync(args[^1]); }
    finally { await fixture.Cleanup(); }
    return;
}

if (args.Contains("--verify-iti-prefix", StringComparer.Ordinal))
{
    var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.FuturesItiStorageBaselineBenchmarks { Mode = "Fast" };
    try { await fixture.Setup(); await fixture.VerifyRecoveredPrefixAsync(args[^1]); }
    finally { await fixture.Cleanup(); }
    return;
}

if (args.Contains("--verify-iti-ingress", StringComparer.Ordinal))
{
    var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.FuturesItiStorageBaselineBenchmarks { Mode = "Fast" };
    try { await fixture.Setup(); await fixture.VerifyOrderedIngressAsync(); }
    finally { await fixture.Cleanup(); }
    return;
}

if (args.Contains("--verify-iti-fast", StringComparer.Ordinal))
{
    foreach (var frame in new[] { TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily,
        TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Weekly,
        TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Monthly })
    {
        var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.FuturesItiStorageBaselineBenchmarks { Mode = "Fast", Frame = frame };
        try { await fixture.Setup(); await fixture.VerifyFastAsync(); }
        finally { await fixture.Cleanup(); }
    }
    return;
}

if (args.Contains("--verify-iti-residency", StringComparer.Ordinal))
{
    foreach (var frame in new[] { TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily,
        TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Weekly,
        TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Monthly })
    foreach (var resident in new[] { false, true })
    {
        var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.FuturesItiStorageBaselineBenchmarks { Resident = resident, Frame = frame };
        try { await fixture.Setup(); await fixture.VerifyLifecycleAsync(); }
        finally { await fixture.Cleanup(); }
    }
    return;
}

if (args.Contains("--verify-api-performance-warmup", StringComparer.Ordinal))
{
    foreach (var scenario in new[] { "Stale", "Retry", "Independent", "SameKey" })
    {
        var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceWarmupBenchmarks { Scenario = scenario };
        fixture.Prepare(); await fixture.OriginalBefore(); await fixture.CurrentAfter();
        Console.WriteLine($"Original/current warmup qualification passed: {scenario}.");
    }
    return;
}


if (args.Contains("--api-performance-seed-resource-probe", StringComparer.Ordinal))
{
    var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceSeedReaderBenchmarks { Trades = 300000 };
    await fixture.Setup();
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(await fixture.ResourceProbeAsync(args.Contains("--candidate", StringComparer.Ordinal))));
    return;
}

if (args.Contains("--verify-api-performance-diagnostic", StringComparer.Ordinal))
{
    var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceDiagnosticBenchmarks { Consumers = 32 };
    try { await fixture.Setup(); await fixture.IndependentBefore(); await fixture.CoalescedAfter(); Console.WriteLine("Production API/Host codec: 32 independent requests versus one shared request, with no per-call test double."); }
    finally { await fixture.Cleanup(); }
    return;
}

if (args.Contains("--verify-api-performance-next-wave", StringComparer.Ordinal))
{
    var indicators = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceIndicatorCommandBenchmarks();
    try
    {
        await indicators.Setup(); indicators.Prepare(); await indicators.SerialBefore(); indicators.VerifyWarmModels();
        indicators.Prepare(); await indicators.BoundedAfter(); indicators.VerifyWarmModels();
        Console.WriteLine("Five real indicator commands persisted warm projections on both paths.");
    }
    finally { await indicators.Cleanup(); }
    foreach (var count in new[] { 1, 4, 32, 128 })
    {
        var restoration = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceTradeRestoreBenchmarks { Trades = count };
        try
        {
            await restoration.Setup(); restoration.Prepare(); await restoration.SerialBefore();
            restoration.Prepare(); await restoration.BoundedAfter();
            Console.WriteLine($"Production NATS/PostgreSQL restoration fixture passed: {count} trades, unchanged contracts and no redundant intent writes.");
        }
        finally { await restoration.Cleanup(); }
    }
    var reader = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceSeedReaderBenchmarks { Trades = 100000 };
    await reader.Setup(); reader.Reset(); _ = await reader.ReadBefore(); reader.Reset(); _ = await reader.ReadAfter();
    Console.WriteLine("Full seed reader fixture passed: identical 48 bars through materialized and streaming paths.");
    return;
}

if (args.Contains("--verify-api-performance-fixtures", StringComparer.Ordinal))
{
    foreach (var bars in new[] { 21, 48, 64 })
    {
        var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceEmaPersistenceBenchmarks { Bars = bars };
        try
        {
            await fixture.Setup();
            fixture.Prepare(); await fixture.PerBarBefore(); fixture.VerifyPersistedRows();
            fixture.Prepare(); await fixture.AtomicBatchAfter(); fixture.VerifyPersistedRows();
            Console.WriteLine($"PostgreSQL EMA fixture passed: {bars} bars, per-bar and atomic batch.");
        }
        finally { await fixture.Cleanup(); }
    }
    var diagnostics = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceDiagnosticBenchmarks { Consumers = 32 };
    try
    {
        await diagnostics.Setup(); await diagnostics.IndependentBefore(); await diagnostics.CoalescedAfter();
        Console.WriteLine("NATS diagnostic fixture passed: 32 requests before, one shared request after.");
    }
    finally { await diagnostics.Cleanup(); }
    var indicatorReads = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceIndicatorReadBenchmarks();
    try
    {
        await indicatorReads.Setup(); await indicatorReads.SerialBefore(); await indicatorReads.BoundedAfter();
        Console.WriteLine("PostgreSQL indicator fixture passed: serial and bounded restoration reads.");
    }
    finally { await indicatorReads.Cleanup(); }
    return;
}

if (args.Contains("--api-performance-resource-probe", StringComparer.Ordinal))
{
    var probe = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiServerPerformanceTradeContentionBenchmarks
    { Producers = int.Parse(args[Array.IndexOf(args, "--producers") + 1], System.Globalization.CultureInfo.InvariantCulture) };
    try
    {
        probe.Setup();
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(probe.ResourceProbe(args.Contains("--candidate", StringComparer.Ordinal))));
    }
    finally { probe.Cleanup(); }
    return;
}

if (args.Contains("--verify-api-optimizations", StringComparer.Ordinal))
{
    var verification = new TomasAI.IFM.Application.Api.Server.Benchmarks.ApiHotPathOptimizationBenchmarks();
    try { verification.Setup(); verification.VerifyEquivalence(); Console.WriteLine("Optimization equivalence checks passed (1,000 randomized snapshots)."); }
    finally { verification.Cleanup(); }
    return;
}
if (args.Contains("--verify-resident-pilots", StringComparer.Ordinal))
{
    foreach (var kind in Enum.GetValues<TomasAI.IFM.TestSupport.ResidentActors.ResidentPilotKind>())
    {
        var fixture = new TomasAI.IFM.Application.Api.Server.Benchmarks.ResidentActorWarmBenchmarks { Kind = kind, Streams = 64 };
        try
        {
            await fixture.Setup(); await fixture.PrepareAsync(); await fixture.Before(); fixture.VerifyCounters();
            await fixture.PrepareAsync(); await fixture.After(); fixture.VerifyCounters();
            Console.WriteLine($"Resident actor paired fixture passed: {kind}, 64 streams, 128 awaited commands per path.");
        }
        finally { await fixture.Cleanup(); }
    }
    return;
}
if (args.Contains("--resident-resource-probe", StringComparer.Ordinal))
{
    var output = args[Array.IndexOf(args, "--output") + 1];
    var seconds = args.Contains("--soak-seconds", StringComparer.Ordinal)
        ? int.Parse(args[Array.IndexOf(args, "--soak-seconds") + 1], System.Globalization.CultureInfo.InvariantCulture) : 0;
    await TomasAI.IFM.Application.Api.Server.Benchmarks.ResidentActorResourceProbe.RunAsync(output, seconds);
    return;
}
if (args.Contains("--resident-child-restart", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.ResidentActorProcessRestartProbe.RunAsync(true,
        Enum.Parse<TomasAI.IFM.TestSupport.ResidentActors.ResidentPilotKind>(args[1]),
        int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture));
    return;
}
if (args.Contains("--verify-resident-process-restart", StringComparer.Ordinal))
{
    await TomasAI.IFM.Application.Api.Server.Benchmarks.ResidentActorProcessRestartProbe.RunAsync(false);
    return;
}
BenchmarkSwitcher.FromAssembly(typeof(TomasAI.IFM.Application.Api.Server.Benchmarks.ActorHealthJsonBenchmarks).Assembly).Run(args);
