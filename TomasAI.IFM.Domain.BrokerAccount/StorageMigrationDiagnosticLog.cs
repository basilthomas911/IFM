namespace TomasAI.IFM.Domain.BrokerAccount;

/// <summary>Compiled structured diagnostics for storage migration and provider boundaries.</summary>
internal static partial class StorageMigrationDiagnosticLog
{
    [global::Microsoft.Extensions.Logging.LoggerMessage(SkipEnabledCheck = true, EventId = 8902, Level = global::Microsoft.Extensions.Logging.LogLevel.Error, Message = "Broker account snapshot handoff failed for {EventId}, {CommandId}, {EntityId}.")]
    internal static partial void Write2(global::Microsoft.Extensions.Logging.ILogger logger, global::System.Exception? exception, global::System.Guid EventId, global::System.Guid CommandId, global::TomasAI.IFM.Domain.BrokerAccount.Contracts.BrokerAccountId EntityId);
}
