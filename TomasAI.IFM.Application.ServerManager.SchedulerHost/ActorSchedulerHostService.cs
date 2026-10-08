using System.Security.Cryptography;
using NATS.Client.Core;
using Quartz;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;
/// <summary>Maintains host observations and consumes durable actor-based Quartz control events.</summary>
public sealed class ActorSchedulerHostService(SchedulerHostOptions options, SchedulerBootstrapState bootstrap,
    IActorProducer producer, IJSActorEventListener listener, IScheduledTaskCommandApi commands, IScheduledTaskQueryApi queries,
    ActorScheduleRuntime runtime, ActorScheduledTaskRecovery recovery, SchedulerOwnershipLease ownership, ISchedulerFactory schedulerFactory,
    ILogger<ActorSchedulerHostService> logger, SchedulerHealthState health,
    TomasAI.IFM.Domain.Reference.Shared.ServiceApi.IReferenceCommandApi referenceCommands,
    TomasAI.IFM.Domain.Reference.Shared.ServiceApi.IReferenceQueryApi referenceQueries) : BackgroundService
{
    private readonly long _generation = DateTimeOffset.UtcNow.UtcTicks;
    private CancellationToken _stopping;
    private bool _functionRegistered;
    private readonly Dictionary<string, (string Fingerprint, string Digest)> _artifactDigests = new(StringComparer.Ordinal);
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.ActorManaged || !bootstrap.Succeeded) return;
        _stopping = stoppingToken;
        var mailbox = new ActorMailboxId(ActorType.Event, ScheduledTaskIdentities.RuntimeActor(options.Environment, options.HostId));
        await producer.StartAsync(mailbox, stoppingToken);
        await listener.StartAsync("scheduler-" + ScheduledTaskIdentities.Catalog(options.Environment, options.HostId).Format(),
            new Dictionary<ActorMailboxId, List<string>> { [mailbox] = [ApplyScheduledTaskRuntimeEvent.Verb, ExecuteScheduledTaskRuntimeRunEvent.Verb] }, AdmitAsync);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    deadline.CancelAfter(TimeSpan.FromSeconds(25));
                    await ownership.EnsureOwnedAsync(deadline.Token);
                    await EnsureFunctionRegistrationAsync(deadline.Token);
                    await PublishCatalogAsync(deadline.Token);
                    var dashboard = await queries.GetScheduledTasksDashboardAsync(new() { Environment = options.Environment, HostId = options.HostId, PageSize = 500 }, deadline.Token);
                    ActorScheduleRuntime.Require(dashboard);
                    foreach (var definition in dashboard.Value!.Schedules)
                    {
                        await recovery.ReconcileAsync(definition, deadline.Token);
                        await runtime.ApplyAsync(definition, deadline.Token);
                    }
                    var freeBytes = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(options.TaskRunRoot))!).AvailableFreeSpace;
                    if (freeBytes < options.MinimumFreeDiskBytes) throw new IOException("Task-run volume is below its configured free-space threshold.");
                    await ownership.EnsureOwnedAsync(deadline.Token);
                    var scheduler = await schedulerFactory.GetScheduler(deadline.Token);
                    if (!scheduler.IsStarted || scheduler.InStandbyMode) await scheduler.Start(deadline.Token);
                    health.Set(TomasAI.IFM.Application.ServerManager.Contracts.SchedulerServiceState.Ready, true, true, true, "Actor-owned Quartz definitions and run reservations reconciled.");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    logger.LogError(exception, "{Component}.{Method} Actor scheduler reconciliation failed for {Environment} {HostId}", nameof(ActorSchedulerHostService), nameof(ExecuteAsync), options.Environment, options.HostId);
                    using var standbyDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    var scheduler = await schedulerFactory.GetScheduler(standbyDeadline.Token);
                    await scheduler.Standby(standbyDeadline.Token);
                    health.Set(TomasAI.IFM.Application.ServerManager.Contracts.SchedulerServiceState.Degraded, true, true, false, "Actor scheduler reconciliation failed; bounded retry pending.");
                }
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
        finally
        {
            await listener.StopAsync();
            await producer.StopAsync();
        }
    }
    /// <summary>Applies a committed control event only on its intended owning host.</summary>
    public async ValueTask AdmitAsync(string verb, NatsMsg<byte[]> message)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(_stopping);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        switch (verb)
        {
            case ApplyScheduledTaskRuntimeEvent.Verb:
                var request = message.AsEvent<ApplyScheduledTaskRuntimeEvent>() ?? throw new InvalidOperationException("Invalid Quartz control payload.");
                if (request.ScheduledTaskDefinition is null || request.EntityId != request.ScheduledTaskDefinition.Id) throw new InvalidOperationException("Runtime definition identity is invalid.");
                await runtime.ApplyAsync(request.ScheduledTaskDefinition, deadline.Token);
                break;
            case ExecuteScheduledTaskRuntimeRunEvent.Verb:
                var manual = message.AsEvent<ExecuteScheduledTaskRuntimeRunEvent>() ?? throw new InvalidOperationException("Invalid manual occurrence payload.");
                if (manual.ScheduledTaskRun is null || manual.EntityId != manual.ScheduledTaskRun.Id) throw new InvalidOperationException("Runtime occurrence identity is invalid.");
                await runtime.TriggerManualAsync(manual.ScheduledTaskRun, deadline.Token);
                break;
            default: throw new InvalidOperationException("Unsupported Quartz control event.");
        }
    }
    /// <summary>Registers the screen through the Reference owner without replacing existing administrator functions.</summary>
    private async Task EnsureFunctionRegistrationAsync(CancellationToken cancellationToken)
    {
        if (_functionRegistered) return;
        var functions = await referenceQueries.GetSystemAdminFunctionTypesAsync().WaitAsync(cancellationToken);
        ActorScheduleRuntime.Require(functions);
        var required = new[] { (Code: "BackupDatabases", Description: "Backups"), (Code: "ScheduledTasks", Description: "Scheduled Tasks") };
        var missing = required.Where(item => !functions.Value!.Any(value => value.ShortCode == item.Code)).ToArray();
        if (missing.Length == 0) { _functionRegistered = true; return; }
        var order = functions.Value!.Select(value => value.OrderId).DefaultIfEmpty(0).Max();
        foreach (var item in missing)
        {
            var registration = await referenceCommands.AddLookupTypeAsync(new(
                "SystemAdminFunctionType", item.Code, ++order, item.Description, DateTime.UtcNow, "QuartzHost")).WaitAsync(cancellationToken);
            if (!registration.Success) throw new InvalidOperationException(registration.ErrorMessage);
        }
        // Read-model confirmation on the next poll avoids treating a command acknowledgement as projection completion.
    }
    /// <summary>Registers deployment facts at startup and refreshes a bounded host capability observation.</summary>
    private async Task PublishCatalogAsync(CancellationToken cancellationToken)
    {
        var id = ScheduledTaskIdentities.Catalog(options.Environment, options.HostId);
        var current = await queries.GetScheduledTaskCatalogAsync(new() { EntityId = id, Environment = options.Environment, HostId = options.HostId }, cancellationToken);
        foreach (var task in options.TaskCatalog)
        {
            var available = task.IsExecutableAvailable(options);
            var digest = task.FileHash ?? "Unavailable";
            if (available)
            {
                var path = task.ResolveExecutablePath(options);
                var files = Directory.EnumerateFiles(Path.GetDirectoryName(path)!).Where(name => Path.GetExtension(name) is ".dll" or ".exe" or ".json")
                    .Select(name => new FileInfo(name)).OrderBy(file => file.Name, StringComparer.Ordinal).ToArray();
                if (files.Length > 2048 || files.Sum(file => file.Length) > 2L * 1024 * 1024 * 1024) throw new IOException("Task artifact exceeds bounded review size.");
                var fingerprint = string.Join(";", files.Select(file => $"{file.Name}:{file.Length}:{file.LastWriteTimeUtc.Ticks}"));
                if (!_artifactDigests.TryGetValue(path, out var cached) || cached.Fingerprint != fingerprint)
                {
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer = new byte[65536];
                    foreach (var file in files)
                    {
                        hash.AppendData(System.Text.Encoding.UTF8.GetBytes(file.Name + "\0"));
                        await using var stream = File.OpenRead(file.FullName);
                        int count;
                        while ((count = await stream.ReadAsync(buffer, cancellationToken)) > 0) hash.AppendData(buffer.AsSpan(0, count));
                    }
                    cached = (fingerprint, Convert.ToHexString(hash.GetHashAndReset()));
                    _artifactDigests[path] = cached;
                }
                digest = cached.Digest;
            }
            var project = new ScheduledTaskProject { TaskKey = task.TaskKey, DisplayName = task.DisplayName, ProjectName = Path.GetFileNameWithoutExtension(task.ExecutablePath), ManifestVersion = task.ManifestVersion,
                Platform = OperatingSystem.IsWindows() ? "Windows" : "Linux", ArtifactDigest = digest, Available = available,
                MaximumRuntimeSeconds = task.MaximumRuntimeSeconds, MarketSensitive = task.RequiresApi };
            if (current.Value?.Projects.SingleOrDefault(p => p.TaskKey == task.TaskKey) == project) continue;
            ActorScheduleRuntime.Require(await commands.RegisterScheduledTaskProjectAsync(new() { CommandId = Guid.NewGuid(), EntityId = id,
                Operator = "QuartzHost", HostId = options.HostId, Environment = options.Environment, Project = project }, cancellationToken));
        }
        var scheduler = await schedulerFactory.GetScheduler(cancellationToken);
        ActorScheduleRuntime.Require(await commands.RecordScheduledTaskHostCapabilityAsync(new() { CommandId = Guid.NewGuid(), EntityId = id,
            Operator = "QuartzHost", HostCapability = new() { HostId = options.HostId, Environment = options.Environment,
                Platform = OperatingSystem.IsWindows() ? "Windows" : "Linux", Ready = ownership.IsOwned && !scheduler.IsShutdown && health.Current.SchedulingStarted,
                ObservedAtUtc = DateTimeOffset.UtcNow, Generation = _generation, Detail = "Lease-owned Quartz runtime is available." } }, cancellationToken));
        // Operational health owns readiness and resumption; catalog reconciliation cannot override standby.
    }
}
