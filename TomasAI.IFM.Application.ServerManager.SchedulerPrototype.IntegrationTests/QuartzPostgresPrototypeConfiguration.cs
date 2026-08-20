using System.Collections.Specialized;

namespace TomasAI.IFM.Application.ServerManager.SchedulerPrototype.IntegrationTests;

internal static class QuartzPostgresPrototypeConfiguration
{
    internal const string DataSourceName = "scheduler";
    internal const string TablePrefix = "ifm_quartz.qrtz_";

    internal static NameValueCollection Create(string connectionString, string schedulerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(schedulerName);

        return new NameValueCollection
        {
            ["quartz.scheduler.instanceName"] = schedulerName,
            ["quartz.scheduler.instanceId"] = "NON_CLUSTERED",
            ["quartz.threadPool.type"] = "Quartz.Simpl.DefaultThreadPool, Quartz",
            ["quartz.threadPool.maxConcurrency"] = "2",
            ["quartz.jobStore.type"] = "Quartz.Impl.AdoJobStore.JobStoreTX, Quartz",
            ["quartz.jobStore.driverDelegateType"] = "Quartz.Impl.AdoJobStore.PostgreSQLDelegate, Quartz",
            ["quartz.jobStore.dataSource"] = DataSourceName,
            ["quartz.jobStore.tablePrefix"] = TablePrefix,
            ["quartz.jobStore.useProperties"] = "true",
            ["quartz.jobStore.clustered"] = "false",
            ["quartz.jobStore.misfireThreshold"] = "60000",
            [$"quartz.dataSource.{DataSourceName}.provider"] = "Npgsql",
            [$"quartz.dataSource.{DataSourceName}.connectionString"] = connectionString,
            ["quartz.serializer.type"] = "stj"
        };
    }
}
