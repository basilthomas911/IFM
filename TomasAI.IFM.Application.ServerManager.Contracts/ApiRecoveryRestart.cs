namespace TomasAI.IFM.Application.ServerManager.Contracts;

/// <summary>A local owned-process request, written only after the recovery error reached the UI event channel.</summary>
public sealed record ApiRecoveryRestartRequest(Guid OperationId, string SessionId, int ProcessId,
    DateTime ProcessStartedUtc, DateOnly ValueDate, DateTime SessionEndUtc, DateTime RequestedUtc, string[] TradeIds,
    DateTime RecoveryDeadlineUtc = default);

public sealed record ApiRecoveryReadiness(int ProcessId, DateOnly? ValueDate, bool FeedsRestored, string[] ProvenTradeIds,
    bool RestartControllerEnabled = false);

public sealed record ApiRecoveryRestartResult(Guid OperationId, DateOnly ValueDate, string State,
    int PreviousProcessId, int? ReplacementProcessId, DateTime CompletedUtc, string? Error);

public static class ApiRecoveryRestartProtocol
{
    public const string DirectoryVariable = "IFM_API_RECOVERY_DIRECTORY";
    public const string SessionVariable = "IFM_DEVELOPMENT_SESSION_ID";
    public const string ControllerVariable = "IFM_API_RECOVERY_CONTROLLER";
    public static string RequestPath(string directory, Guid id) => Path.Combine(directory, $"request-{id:N}.json");
    public static string ResultPath(string directory, Guid id) => Path.Combine(directory, $"result-{id:N}.json");
}
