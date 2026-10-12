using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Framework.Storage;

internal static partial class StorageLog
{
    [LoggerMessage(101, LogLevel.Debug, "ObjectDataRepository.UseCommandText: command name: {CommandName}{NewLine}{CommandText}")]
    internal static partial void UseCommand(ILogger logger, string commandName, string newLine, string commandText);

    [LoggerMessage(102, LogLevel.Debug, "ObjectDataRepository.UseUri: command name: {CommandName}{NewLine}{Uri}")]
    internal static partial void UseUri(ILogger logger, string commandName, string newLine, Uri uri);

    [LoggerMessage(103, LogLevel.Debug, "{ClassName}.ExecuteCommandAsync: {CommandText} with {ParameterValuesCount} parameter values")]
    internal static partial void ExecuteCommand(ILogger logger, string className, string commandText, int parameterValuesCount);

    [LoggerMessage(104, LogLevel.Debug, "{ClassName}.ExecuteQueuedCommandsAsync: {CommandText} with {BindValuesCount} bind values")]
    internal static partial void ExecuteQueuedCommand(ILogger logger, string className, string commandText, int bindValuesCount);

    [LoggerMessage(105, LogLevel.Warning, "{ClassName}.ExecuteQueuedCommandsAsync is executing an explicit logged batch containing {StatementCount} statements; keep atomic batches small and partition-local where possible")]
    internal static partial void LargeBatch(ILogger logger, string className, int statementCount);
}
