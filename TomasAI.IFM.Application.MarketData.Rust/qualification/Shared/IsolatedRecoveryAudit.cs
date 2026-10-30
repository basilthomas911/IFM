namespace TomasAI.IFM.Application.MarketData.Rust.Qualification;

/// <summary>Runs before a qualification executable opens any native runtime.
/// An explicitly supplied test location is preserved; ordinary fixture runs get
/// a process-specific temporary outbox instead of the live Development queue.</summary>
internal static class IsolatedRecoveryAudit
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Initialize()
    {
        const string key = "IFM_MARKETDATA_RECOVERY_OUTBOX";
        if (!string.IsNullOrWhiteSpace(System.Environment.GetEnvironmentVariable(key))) return;
        System.Environment.SetEnvironmentVariable(key, System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "ifm-native-qualification-audit",
            System.Environment.ProcessId + "-" + System.Guid.NewGuid().ToString("N")));
    }
}
