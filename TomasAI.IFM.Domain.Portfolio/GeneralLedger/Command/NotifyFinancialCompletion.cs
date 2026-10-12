using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;

/// <summary>Projection retry cannot turn a confirmed financial commit into a reported financial failure.</summary>
public static class NotifyFinancialCompletion
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")]     public static async ValueTask<ServiceResult<GuidResult>> NotifyAsync<TActor>(this IFinancialCompletedEvent completed,
        IEventProjector<TActor> projector, ILogger logger) where TActor : ICommandActor<TActor>
    {
        ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(projector);
        try { await (projector.DomainEventsProjectionAsync(new DomainEventCollection([completed]))).ConfigureAwait(false); }
        catch (Exception error) { global::TomasAI.IFM.Domain.Portfolio.ActorLifecycleDiagnosticLog.Write3(logger, error, nameof(NotifyFinancialCompletion), nameof(NotifyAsync), completed.OperationId); }
        return new ServiceOk<GuidResult>(new(completed.OperationId));
    }
}
