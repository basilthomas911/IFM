using Microsoft.Extensions.Configuration;
using Npgsql;
using Quartz;
using Serilog;
using TomasAI.IFM.Framework.Telemetry.Logging;
using TomasAI.IFM.Framework.Telemetry.Metrics;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public static class SchedulerHostApplication
{
    public static IHost Create(string[] args, Action<ConfigurationManager>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder(args);
        configure?.Invoke(builder.Configuration);
        var logging = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).Enrich.FromLogContext()
            .WriteTo.Console();
        if (builder.Configuration.GetValue<bool>("Telemetry:Logs:Enabled"))
            logging.WriteTo.Sink(new OtlpStructuredLogSink(builder.Configuration, "IFM-SchedulerHost"));
        builder.Services.AddSerilog(logging.CreateLogger(), dispose: true);
        builder.Services.AddIfmMetrics(builder.Configuration, "IFM-SchedulerHost");
        if (OperatingSystem.IsWindows())
            builder.Services.AddWindowsService(options => options.ServiceName = "IFM Scheduler Host");

        var schedulerOptions = builder.Configuration.GetSection("SchedulerHost").Get<SchedulerHostOptions>()
            ?? throw new InvalidOperationException("The SchedulerHost configuration section is missing.");
        schedulerOptions.Validate();
        var connectionString = builder.Configuration.GetConnectionString("SchedulerDbConnection");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        builder.Services.AddSingleton(schedulerOptions);
        builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        builder.Services.AddSingleton<SchedulerOwnershipLease>();
        builder.Services.AddSingleton<SchedulerHealthState>();
        builder.Services.AddSingleton<SchedulerBootstrapState>();
        builder.Services.AddSingleton<SchedulerDatabaseMigrator>();
        builder.Services.AddSingleton<TaskCatalogProvider>();
        builder.Services.AddSingleton<SchedulerStore>();
        builder.Services.AddHttpClient();
        builder.Services.AddSingleton<DependencyProbeService>();
        builder.Services.AddSingleton<ActiveRunRegistry>();
        builder.Services.AddSingleton<ScheduledProcessRunner>();
        builder.Services.AddSingleton<ScheduledTaskExecutionService>();
        builder.Services.AddSingleton<QuartzScheduleReconciler>();
        builder.Services.AddSingleton<ScheduleValidationService>();
        builder.Services.AddSingleton<ScheduleSeedProvider>();
        builder.Services.AddSingleton<SchedulerOutputService>();
        builder.Services.AddSingleton<SchedulerRetentionService>();
        builder.Services.AddSingleton<SchedulerOperationsService>();
        builder.Services.AddSingleton<SchedulerDashboardQueryService>();

        builder.Services.AddQuartz(quartz =>
        {
            quartz.SchedulerName = schedulerOptions.SchedulerName;
            quartz.UseDefaultThreadPool(threadPool => threadPool.MaxConcurrency = schedulerOptions.MaximumConcurrentProcesses);
            quartz.UsePersistentStore(store =>
            {
                store.UseProperties = true;
                store.PerformSchemaValidation = true;
                store.UsePostgres(postgres =>
                {
                    postgres.ConnectionString = connectionString;
                    postgres.TablePrefix = "ifm_quartz.qrtz_";
                });
                store.UseSystemTextJsonSerializer();
            });
        });

        builder.Services.AddHostedService<SchedulerBootstrapService>();
        builder.Services.AddHostedService<SchedulerRuntimeService>();
        builder.Services.AddHostedService<SchedulerOperationalMonitor>();
        builder.Services.AddHostedService<SchedulerRunRequestDispatcher>();
        builder.Services.AddHostedService<SchedulerRetentionHostedService>();
        if (schedulerOptions.ActorManaged) builder.Services.AddSchedulerActorServices(builder.Configuration);
        if (OperatingSystem.IsWindows()) builder.Services.AddHostedService<SchedulerPipeServer>();
        return builder.Build();
    }
}
