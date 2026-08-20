using FluentAssertions;
using Xunit;

namespace TomasAI.IFM.Application.ServerManager.SchedulerPrototype.IntegrationTests;

public sealed class QuartzPostgresPrototypeConfigurationTests
{
    [Fact]
    public void Configuration_uses_the_approved_non_clustered_postgresql_store()
    {
        var properties = QuartzPostgresPrototypeConfiguration.Create(
            "Host=localhost;Database=ifm_scheduler;Username=ifm_scheduler",
            "IFM-Scheduler-Prototype");

        properties["quartz.scheduler.instanceId"].Should().Be("NON_CLUSTERED");
        properties["quartz.jobStore.type"].Should().Be("Quartz.Impl.AdoJobStore.JobStoreTX, Quartz");
        properties["quartz.jobStore.driverDelegateType"].Should().Be(
            "Quartz.Impl.AdoJobStore.PostgreSQLDelegate, Quartz");
        properties["quartz.jobStore.dataSource"].Should().Be(
            QuartzPostgresPrototypeConfiguration.DataSourceName);
        properties["quartz.jobStore.tablePrefix"].Should().Be(
            QuartzPostgresPrototypeConfiguration.TablePrefix);
        properties["quartz.jobStore.useProperties"].Should().Be("true");
        properties["quartz.jobStore.clustered"].Should().Be("false");
        properties["quartz.dataSource.scheduler.provider"].Should().Be("Npgsql");
        properties["quartz.serializer.type"].Should().Be("stj");
    }

    [Theory]
    [InlineData(null, "scheduler")]
    [InlineData("", "scheduler")]
    [InlineData("connection", null)]
    [InlineData("connection", "")]
    public void Configuration_rejects_missing_required_values(string? connectionString, string? schedulerName)
    {
        var action = () => QuartzPostgresPrototypeConfiguration.Create(connectionString!, schedulerName!);

        action.Should().Throw<ArgumentException>();
    }
}
