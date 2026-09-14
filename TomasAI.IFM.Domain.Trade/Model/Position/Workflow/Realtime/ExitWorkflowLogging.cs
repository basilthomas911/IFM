using Microsoft.Extensions.Logging;

namespace TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Realtime;

internal static partial class ExitWorkflowLogging
{
    [LoggerMessage(EventId = 27290, Level = LogLevel.Critical,
        Message = "{Strategy} exit workflow failed for {WorkflowId}.")]
    internal static partial void Failed(ILogger logger, Exception exception,
        string strategy, string workflowId);
}
