using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Persistence;

internal static partial class ExitWorkflowProjectionSubmission
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "After commit, projection submission failure is recovered from the durable marker and cannot reject the accepted command.")]
    internal static async ValueTask SubmitAsync(Func<DomainEventCollection, ValueTask> submit, DomainEventCollection pending, ILogger logger)
    {
        try
        {
            await submit(pending).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            SubmissionFailed(logger, exception);
        }
    }

    [LoggerMessage(EventId = 27229, Level = LogLevel.Warning,
        Message = "Exit workflow state committed. Its durable projection marker will recover failed submission.")]
    private static partial void SubmissionFailed(ILogger logger, Exception exception);
}
