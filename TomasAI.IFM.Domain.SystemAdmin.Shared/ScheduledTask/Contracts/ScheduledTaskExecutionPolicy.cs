namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

/// <summary>Defines the temporary Development market lifecycle execution policy.</summary>
public static class ScheduledTaskExecutionPolicy
{
    /// <summary>Development market lifecycle executes without runtime admission gates.</summary>
    public static bool RequiresAdmission(string taskKey, string environment)
        => !((string.Equals(taskKey, ScheduledTaskKeys.FuturesMarketOpen, StringComparison.Ordinal)
                || string.Equals(taskKey, ScheduledTaskKeys.FuturesMarketClose, StringComparison.Ordinal))
            && string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase));
}
