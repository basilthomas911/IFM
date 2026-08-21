using FluentAssertions;
using Npgsql;
using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Application.ServerManager.SchedulerHost;

namespace TomasAI.IFM.Application.ServerManager.UnitTests;

public sealed class ScheduleOperationsTests
{
    [Fact]
    public void Cron_validation_returns_ten_timezone_aware_fire_times()
    {
        var validator = CreateValidator(SchedulerRiskClassification.Maintenance, maximumRuntimeSeconds: 600);

        var result = validator.Validate(Input("0 0 2 ? * MON-FRI"));

        result.IsValid.Should().BeTrue();
        result.NextFireTimes.Should().HaveCount(10);
        result.NextFireTimes.Should().OnlyContain(value => value.TimeZoneId == "America/New_York");
        result.Explanation.Should().Contain("Cron");
    }

    [Fact]
    public void Market_sensitive_schedule_rejects_fire_once_now_misfire()
    {
        var validator = CreateValidator(SchedulerRiskClassification.TradingSensitive, maximumRuntimeSeconds: 600);

        var result = validator.Validate(Input("0 0 16 ? * MON-FRI") with
        {
            MisfirePolicy = SchedulerMisfirePolicy.FireOnceNow
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(value => value.Contains("DoNothing"));
    }

    [Fact]
    public void Validation_enforces_catalog_runtime_and_retention_limits()
    {
        var validator = CreateValidator(SchedulerRiskClassification.Maintenance, maximumRuntimeSeconds: 60);

        var result = validator.Validate(Input("120") with
        {
            Kind = ScheduleKind.SimpleInterval,
            MaximumRuntimeSeconds = 61,
            SuccessfulRetentionDays = 200,
            FailedRetentionDays = 100
        });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Output_redaction_removes_common_inline_secret_values()
    {
        var result = SchedulerOutputService.Redact("token=abc password = hunter2 safe=value");

        result.Should().NotContain("abc").And.NotContain("hunter2");
        result.Should().Contain("token=<redacted>").And.Contain("password=<redacted>");
        result.Should().Contain("safe=value");
    }

    [Fact]
    public void Active_run_registry_targets_only_the_owned_run()
    {
        var registry = new ActiveRunRegistry();
        var firstId = Guid.NewGuid();
        using var first = registry.Register(firstId, CancellationToken.None);
        using var second = registry.Register(Guid.NewGuid(), CancellationToken.None);

        registry.RequestCancellation(firstId).Should().BeTrue();

        first.Token.IsCancellationRequested.Should().BeTrue();
        second.Token.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void Configuration_refuses_enabled_seed_schedule()
    {
        var options = CreateOptions(SchedulerRiskClassification.Maintenance, 60);
        options.InitialSchedules.Add(new InitialScheduleDefinition
        {
            ScheduleDefinitionId = Guid.NewGuid(),
            Name = "unsafe",
            TaskKey = "task",
            Enabled = true,
            Kind = ScheduleKind.Cron,
            ScheduleExpression = "0 0 0 ? * MON-FRI"
        });

        var action = options.Validate;

        action.Should().Throw<InvalidOperationException>().WithMessage("*disabled*");
    }

    private static ScheduleValidationService CreateValidator(
        SchedulerRiskClassification risk,
        int maximumRuntimeSeconds)
    {
        var options = CreateOptions(risk, maximumRuntimeSeconds);
        options.Validate();
        var dataSource = NpgsqlDataSource.Create("Host=localhost;Database=unused;Username=unused");
        return new ScheduleValidationService(options, new TaskCatalogProvider(options, dataSource));
    }

    private static SchedulerHostOptions CreateOptions(
        SchedulerRiskClassification risk,
        int maximumRuntimeSeconds)
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-validator-root");
        return new SchedulerHostOptions
        {
            DeploymentRoot = root,
            TaskRunRoot = Path.Combine(root, "runs"),
            TaskCatalog =
            [
                new ScheduledTaskCatalogDefinition
                {
                    TaskKey = "task",
                    DisplayName = "Task",
                    Description = "Test task",
                    ExecutablePath = "task.exe",
                    WorkingDirectory = ".",
                    RiskClassification = risk,
                    MaximumRuntimeSeconds = maximumRuntimeSeconds
                }
            ]
        };
    }

    private static ScheduleDefinitionInputDto Input(string expression)
        => new(
            null,
            "test schedule",
            "test",
            "task",
            ScheduleKind.Cron,
            expression,
            "America/New_York",
            SchedulerMisfirePolicy.DoNothing,
            60);
}
