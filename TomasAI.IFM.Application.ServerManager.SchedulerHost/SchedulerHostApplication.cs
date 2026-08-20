using Microsoft.Extensions.Configuration;
using Npgsql;
using Quartz;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public static class SchedulerHostApplication
{
    public static IHost Create(string[] args, Action<ConfigurationManager>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder(args);
        configure?.Invoke(builder.Configuration);
        builder.Services.AddWindowsService(options => options.ServiceName = "IFM Scheduler Host");

        var schedulerOptions = builder.Configuration.GetSection("SchedulerHost").Get<SchedulerHostOptions>()
            ?? throw new InvalidOperationException("The SchedulerHost configuration section is missing.");
        schedulerOptions.Validate();
        var connectionString = builder.Configuration.GetConnectionString("SchedulerDbConnection");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        builder.Services.AddSingleton(schedulerOptions);
        builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        builder.Services.AddSingleton<SchedulerHealthState>();
        builder.Services.AddSingleton<SchedulerBootstrapState>();
        builder.Services.AddSingleton<SchedulerDatabaseMigrator>();
        builder.Services.AddSingleton<TaskCatalogProvider>();
        builder.Services.AddSingleton<SchedulerStore>();
        builder.Services.AddSingleton<ScheduledProcessRunner>();
        builder.Services.AddSingleton<ScheduledTaskExecutionService>();
        builder.Services.AddSingleton<QuartzScheduleReconciler>();
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
        builder.Services.AddHostedService<SchedulerPipeServer>();
        return builder.Build();
    }
}
