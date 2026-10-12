namespace TomasAI.IFM.Domain.Supervisor;

/// <summary>Compiled structured diagnostics for storage migration and provider boundaries.</summary>
internal static partial class StorageMigrationDiagnosticLog
{
    [global::Microsoft.Extensions.Logging.LoggerMessage(SkipEnabledCheck = true, EventId = 8901, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "Supervisor operation {Operation} failed for {Target} at {Stage}: {FailureReason}")]
    internal static partial void Write1(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, global::TomasAI.IFM.Domain.Supervisor.Shared.Enums.SupervisorActorOperationKind Operation, global::TomasAI.IFM.Shared.EventModelActor.ActorThreadId Target, string Stage, string FailureReason);
}
