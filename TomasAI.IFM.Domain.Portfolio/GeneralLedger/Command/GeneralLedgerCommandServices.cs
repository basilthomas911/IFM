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

public sealed record GeneralLedgerCommandServices(IGeneralLedgerStore Store,IPortfolioFinancialDbContext Database,
    FinancialIdentityAllocator Ids,IEventProjector<GeneralLedgerCommandActor> Projector,ILogger<GeneralLedgerCommandActor> Logger);

/// <summary>Projection retry cannot turn a confirmed financial commit into a reported financial failure.</summary>
public static class NotifyFinancialCompletion
{
    public static async ValueTask<ServiceResult<GuidResult>> NotifyAsync<TActor>(this IFinancialCompletedEvent completed,
        IEventProjector<TActor> projector,ILogger logger) where TActor:ICommandActor<TActor>
    {
        try { await projector.DomainEventsProjectionAsync(new DomainEventCollection([completed])); }
        catch(Exception error) { logger.LogError(error,"Financial operation {OperationId} committed; durable history notification is pending.",completed.OperationId); }
        return new ServiceOk<GuidResult>(new(completed.OperationId));
    }
}
