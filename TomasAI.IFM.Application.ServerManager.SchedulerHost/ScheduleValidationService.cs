using System.Globalization;
using Quartz;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed class ScheduleValidationService(
    SchedulerHostOptions options,
    TaskCatalogProvider catalog)
{
    public ScheduleValidationResultDto Validate(ScheduleDefinitionInputDto input)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var previews = new List<ScheduleFirePreviewDto>();
        ScheduledTaskCatalogDefinition? task = null;
        try
        {
            task = catalog.GetRequired(input.TaskKey);
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
        }

        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 200)
        {
            errors.Add("Schedule name is required and cannot exceed 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(input.TimeZoneId))
        {
            errors.Add("An explicit timezone is required.");
        }

        TimeZoneInfo? timeZone = null;
        if (errors.All(value => !value.Contains("timezone", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(input.TimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                errors.Add($"Timezone '{input.TimeZoneId}' is not installed on this host.");
            }
            catch (InvalidTimeZoneException)
            {
                errors.Add($"Timezone '{input.TimeZoneId}' is invalid on this host.");
            }
        }

        if (task is not null && input.MaximumRuntimeSeconds is <= 0)
        {
            errors.Add("Maximum runtime must be positive when specified.");
        }
        else if (task is not null && input.MaximumRuntimeSeconds > task.MaximumRuntimeSeconds)
        {
            errors.Add($"Maximum runtime cannot exceed the catalog limit of {task.MaximumRuntimeSeconds} seconds.");
        }

        if (input.SuccessfulRetentionDays is < 1 or > 365
            || input.FailedRetentionDays is < 1 or > 3650
            || input.SuccessfulRetentionDays > input.FailedRetentionDays)
        {
            errors.Add("Retention must be 1-365 days for successful runs, 1-3650 days for failures, and failure retention cannot be shorter.");
        }

        if (task?.RiskClassification is SchedulerRiskClassification.MarketLifecycle or SchedulerRiskClassification.TradingSensitive
            && input.MisfirePolicy != SchedulerMisfirePolicy.DoNothing)
        {
            errors.Add("Market-sensitive schedules must use the DoNothing misfire policy.");
        }

        if (timeZone is not null)
        {
            ValidateExpression(input, timeZone, errors, previews);
        }

        if (input.Kind == ScheduleKind.Cron && !string.Equals(input.TimeZoneId, "America/New_York", StringComparison.OrdinalIgnoreCase)
            && task?.RiskClassification is SchedulerRiskClassification.MarketLifecycle or SchedulerRiskClassification.TradingSensitive)
        {
            warnings.Add("Market schedules normally use America/New_York; confirm the approved exchange/session timezone.");
        }

        if (task?.RequiresApi == true)
        {
            warnings.Add("The task will be recorded as BlockedDependency when its API or endpoint prerequisites are unavailable.");
        }

        var explanation = errors.Count == 0
            ? Explain(input)
            : "Schedule is invalid.";
        return new ScheduleValidationResultDto(errors.Count == 0, explanation, errors, warnings, previews);
    }

    private static void ValidateExpression(
        ScheduleDefinitionInputDto input,
        TimeZoneInfo timeZone,
        ICollection<string> errors,
        ICollection<ScheduleFirePreviewDto> previews)
    {
        try
        {
            switch (input.Kind)
            {
                case ScheduleKind.Cron:
                {
                    var cron = new CronExpression(input.ScheduleExpression) { TimeZone = timeZone };
                    var cursor = DateTimeOffset.UtcNow;
                    for (var index = 0; index < 10; index++)
                    {
                        var next = cron.GetNextValidTimeAfter(cursor);
                        if (next is null)
                        {
                            break;
                        }

                        previews.Add(new ScheduleFirePreviewDto(
                            next.Value,
                            TimeZoneInfo.ConvertTime(next.Value, timeZone),
                            timeZone.Id));
                        cursor = next.Value;
                    }

                    break;
                }
                case ScheduleKind.SimpleInterval:
                {
                    if (!int.TryParse(input.ScheduleExpression, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
                        || seconds < 60 || seconds > 31_536_000)
                    {
                        errors.Add("Simple interval must be an integer from 60 through 31536000 seconds.");
                        break;
                    }

                    var cursor = DateTimeOffset.UtcNow;
                    for (var index = 1; index <= 10; index++)
                    {
                        var next = cursor.AddSeconds((long)seconds * index);
                        previews.Add(new ScheduleFirePreviewDto(next, TimeZoneInfo.ConvertTime(next, timeZone), timeZone.Id));
                    }

                    break;
                }
                case ScheduleKind.OneTime:
                {
                    if (!DateTimeOffset.TryParse(input.ScheduleExpression, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var instant))
                    {
                        errors.Add("One-time expression must be an ISO-8601 timestamp with an explicit offset.");
                    }
                    else if (instant <= DateTimeOffset.UtcNow)
                    {
                        errors.Add("One-time schedule must be in the future.");
                    }
                    else
                    {
                        previews.Add(new ScheduleFirePreviewDto(instant.ToUniversalTime(), TimeZoneInfo.ConvertTime(instant, timeZone), timeZone.Id));
                    }

                    break;
                }
                default:
                    errors.Add($"Schedule kind '{input.Kind}' is unsupported.");
                    break;
            }
        }
        catch (FormatException exception)
        {
            errors.Add($"Schedule expression is invalid: {exception.Message}");
        }
    }

    private static string Explain(ScheduleDefinitionInputDto input)
        => input.Kind switch
        {
            ScheduleKind.Cron => $"Cron '{input.ScheduleExpression}' in {input.TimeZoneId} ({input.MisfirePolicy} on misfire).",
            ScheduleKind.SimpleInterval => $"Every {input.ScheduleExpression} seconds ({input.MisfirePolicy} on misfire).",
            ScheduleKind.OneTime => $"Once at {input.ScheduleExpression} ({input.TimeZoneId}).",
            _ => input.ScheduleExpression
        };
}
