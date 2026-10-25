using Microsoft.Extensions.Logging;
namespace TomasAI.IFM.Application.CommandPipeline;
internal static partial class CommandFailureLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "{CommandName}: execution failed")]
    internal static partial void Failed(ILogger logger, Exception exception, string commandName);
}
