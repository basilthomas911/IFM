using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Projection;

/// <summary>Submits committed events whose PostgreSQL markers survive an enqueue failure.</summary>
internal static class PortfolioProjectionSubmission
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "A durable marker makes a post-commit submission failure recoverable without reporting the committed command as failed.")]
    internal static async ValueTask SubmitAsync(IEventProjector projector, IEvent source, ILogger logger)
    {
        try
        {
            await projector.DomainEventsProjectionAsync(new DomainEventCollection([source])).ConfigureAwait(false);
        }
        catch (Exception exception) when (source is IRequireDurableProjection { RequiresDurableProjection: true, })
        {
            SubmissionFailed(logger, source.Id, source.CommandId, exception);
        }
    }

    static readonly Action<ILogger, Guid, Guid, Exception?> SubmissionFailed = LoggerMessage.Define<Guid, Guid>(
        LogLevel.Error, new EventId(1, nameof(SubmitAsync)),
        "Portfolio event {SourceEventId} for command {CommandId} committed and is awaiting projector recovery.");
}
