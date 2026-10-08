using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Application.ServerManager.SchedulerHost;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

// This tool mutates definitions only through their command actors. PostgreSQL is read for adoption/export only.
if (args.Length < 2) throw new ArgumentException("Usage: <settings.json> status|market-status|prepare|adopt|enable|disable|run|qualify-open|remove [schedule-id] [export.json]");
var configuration = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(args[0]), false).AddEnvironmentVariables().Build();
var options = configuration.GetSection("SchedulerHost").Get<SchedulerHostOptions>() ?? throw new InvalidOperationException("Scheduler settings missing.");
if (!options.ActorManaged) throw new InvalidOperationException("Stop the legacy writer and select ActorManaged before using this tool.");
using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
var token = deadline.Token;
await using var connections = new NatsConnectionManager();
var producer = new NatsActorProducer(configuration.GetSection("Nats:Producer").Get<NatsProducerOptions>() ?? new(), NullLogger.Instance, connections);
await producer.StartAsync(new(ActorType.Command, "ScheduledTaskAdministration"), token);
var commands = new ScheduledTaskCommandApi(producer);
var queries = new ScheduledTaskQueryApi(producer);
var action = args[1];
ScheduledTaskId selected = default;
var dashboard = Require(await queries.GetScheduledTasksDashboardAsync(new() { Environment = options.Environment, HostId = options.HostId, PageSize = 500 }, token));
if (action == "market-status")
{
    var session = Require(await new MarketDataQueryApi(producer).GetMarketSessionAsync().WaitAsync(token));
    Console.WriteLine(JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true }));
    return;
}
if (action == "prepare")
{
    foreach (var seed in options.InitialSchedules)
    {
        if (dashboard.Schedules.Any(x => x.Id.Value == seed.ScheduleDefinitionId)) continue;
        var schedule = new ScheduledTaskSchedule { TaskKey = seed.TaskKey, Name = seed.Name, Description = seed.Description,
            Environment = options.Environment, HostId = options.HostId, Timing = seed.Kind == ScheduleKind.Cron ? ScheduledTaskTiming.Cron : ScheduledTaskTiming.OneTime,
            Expression = seed.ScheduleExpression, TimeZoneId = seed.TimeZoneId, MaximumRuntimeSeconds = seed.MaximumRuntimeSeconds ?? 1800 };
        Require(await commands.CreateScheduledTaskAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(seed.ScheduleDefinitionId), Schedule = schedule, Operator = "DevelopmentDeployment" }, token));
        Console.WriteLine($"Prepared disabled definition {seed.ScheduleDefinitionId:D} {seed.TaskKey}");
    }
}
else if (action == "adopt")
{
    if (args.Length < 3) throw new ArgumentException("Adoption requires an export destination.");
    await using var data = NpgsqlDataSource.Create(configuration.GetConnectionString("SchedulerDbConnection")!);
    var legacy = await new SchedulerStore(data, options).GetSchedulesAsync(token);
    await File.WriteAllTextAsync(Path.GetFullPath(args[2]), JsonSerializer.Serialize(legacy, new JsonSerializerOptions { WriteIndented = true }), token);
    foreach (var row in legacy)
    {
        if (dashboard.Schedules.Any(x => x.Id.Value == row.ScheduleDefinitionId)) continue;
        if (row.Kind != ScheduleKind.Cron) throw new InvalidOperationException($"Review one-time legacy schedule {row.ScheduleDefinitionId} before adoption; its historic occurrence must not be replayed.");
        Require(await commands.CreateScheduledTaskAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(row.ScheduleDefinitionId), Operator = "LegacyAdoption",
            Schedule = new() { TaskKey = row.TaskKey, Name = row.Name, Description = row.Description, Environment = options.Environment, HostId = options.HostId,
                Expression = row.ScheduleExpression, TimeZoneId = row.TimeZoneId, MaximumRuntimeSeconds = row.MaximumRuntimeSeconds ?? 1800 } }, token));
        Console.WriteLine($"Adopted disabled {row.ScheduleDefinitionId:D}; prior enabled={row.Enabled}. Review deployment and enable explicitly.");
    }
}
else if (action == "qualify-open")
{
    selected = new(Guid.NewGuid());
    var fire = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddSeconds(60).ToUnixTimeSeconds());
    Require(await commands.CreateScheduledTaskAsync(new() { CommandId = Guid.NewGuid(), EntityId = selected, Operator = "DevelopmentQualification",
        Schedule = new() { TaskKey = ScheduledTaskKeys.FuturesMarketOpen, Name = "Opening Qualification (one-time)", Environment = options.Environment,
            HostId = options.HostId, Timing = ScheduledTaskTiming.OneTime, StartsAtUtc = fire, TimeZoneId = "America/New_York", MaximumRuntimeSeconds = 900,
            Description = "Explicit current-session scheduled occurrence; remove after reviewing its terminal outcome." } }, token));
    ScheduledTaskDefinition? definition = null;
    for (var attempt = 0; attempt < 20; attempt++)
    {
        var projected = await queries.GetScheduledTaskAsync(new() { EntityId = selected, Environment = options.Environment, HostId = options.HostId }, token);
        if (projected.Success && projected.Value is { } value) { definition = value; break; }
        await Task.Delay(250, token);
    }
    if (definition is null) throw new InvalidOperationException("Qualification definition projection timed out; inspect before enabling.");
    Require(await commands.EnableScheduledTaskAsync(new() { CommandId = Guid.NewGuid(), EntityId = selected, ExpectedRevision = definition.Revision,
        Operator = "DevelopmentQualification", Reason = "Verify actual automatic Quartz occurrence after cron guard correction" }, token));
    Console.WriteLine($"Qualification schedule {selected.Value:D} fires at {fire:O}.");
}
else if (action == "remove")
{
    if (args.Length < 4 || !Guid.TryParse(args[2], out var id)) throw new ArgumentException("Removal requires a schedule UUID and an operator reason.");
    var definition = dashboard.Schedules.Single(x => x.Id.Value == id);
    Require(await commands.RemoveScheduledTaskAsync(new() { CommandId = Guid.NewGuid(), EntityId = definition.Id, ExpectedRevision = definition.Revision,
        Operator = "DevelopmentOperator", Reason = args[3] }, token));
}
else if (action is "enable" or "disable")
{
    if (args.Length < 3 || !Guid.TryParse(args[2], out var id)) throw new ArgumentException("A stable schedule UUID is required.");
    var definition = dashboard.Schedules.Single(x => x.Id.Value == id);
    if (action == "enable") Require(await commands.EnableScheduledTaskAsync(new() { CommandId = Guid.NewGuid(), EntityId = definition.Id, ExpectedRevision = definition.Revision, Operator = "DevelopmentOperator", Reason = "Reviewed deployed task" }, token));
    else Require(await commands.DisableScheduledTaskAsync(new() { CommandId = Guid.NewGuid(), EntityId = definition.Id, ExpectedRevision = definition.Revision, Operator = "DevelopmentOperator" }, token));
}
else if (action == "run")
{
    if (args.Length < 4 || !Guid.TryParse(args[2], out var id) || string.IsNullOrWhiteSpace(args[3]))
        throw new ArgumentException("Run Now requires a stable schedule UUID and an operator reason.");
    var definition = dashboard.Schedules.Single(x => x.Id.Value == id);
    var runId = Guid.NewGuid();
    Require(await commands.RequestScheduledTaskRunAsync(new() { CommandId = Guid.NewGuid(), EntityId = new(runId), ScheduleId = definition.Id,
        DefinitionRevision = definition.DesiredRevision, IntendedFireTimeUtc = DateTimeOffset.UtcNow, Manual = true,
        TaskKey = definition.Schedule.TaskKey, HostId = options.HostId, Environment = options.Environment,
        Operator = "DevelopmentOperator", Reason = args[3] }, token));
    Console.WriteLine($"Requested Quartz manual occurrence {runId:D}; query its persisted terminal result before another run.");
}
else if (action != "status") throw new ArgumentException("Unknown administration action.");
if (action == "status" && args.Length > 2 && Guid.TryParse(args[2], out var selectedId)) selected = new(selectedId);
var latest = Require(await queries.GetScheduledTasksDashboardAsync(new() { Environment = options.Environment, HostId = options.HostId, PageSize = 500, SelectedScheduleId = selected }, token));
Console.WriteLine(JsonSerializer.Serialize(latest, new JsonSerializerOptions { WriteIndented = true }));

static T Require<T>(ServiceResult<T> result) where T : class => result.Success && result.Value is not null ? result.Value : throw new InvalidOperationException(result.ErrorMessage);
