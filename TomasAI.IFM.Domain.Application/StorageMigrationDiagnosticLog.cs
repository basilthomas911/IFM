namespace TomasAI.IFM.Domain.Application;

/// <summary>Compiled structured diagnostics for storage migration and provider boundaries.</summary>
internal static partial class StorageMigrationDiagnosticLog
{
    [global::Microsoft.Extensions.Logging.LoggerMessage(SkipEnabledCheck = true, EventId = 8903, Level = global::Microsoft.Extensions.Logging.LogLevel.Information, Message = "Application startup activity {Activity} returned {Outcome}. Required={Required}; ValueDate={ValueDate}; CommandId={CommandId}; CorrelationId={CorrelationId}; Reason={Reason}")]
    internal static partial void Write3(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, global::TomasAI.IFM.Domain.Application.Shared.ApplicationStartupActivity Activity, global::TomasAI.IFM.Domain.Application.Shared.ApplicationStartupActivityOutcome Outcome, bool Required, global::System.DateOnly ValueDate, global::System.Guid CommandId, global::System.Guid CorrelationId, string Reason);
}
