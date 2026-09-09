using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Command;

public sealed record CapacityReservationCommandServices(ICapacityReservationStore Store,IPortfolioFinancialDbContext Database,
    IEventProjector<CapacityReservationCommandActor> Projector,ILogger<CapacityReservationCommandActor> Logger);

public static class ChangeCapacityReservation
{
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this ChangeCapacityReservationCommand request,CapacityReservationCommandServices services,CancellationToken token)
    {
        var replay=await services.Database.ReadOperationAsync<CapacityLifecycleCompletedEvent>(request.PortfolioId,request.OperationId,request.InputSha256,token);
        if(replay is not null) return await replay.NotifyAsync(services.Projector,services.Logger);
        FinancialRequestValidation.Demand(request,"CapacityLifecycle",DateTime.UtcNow);
        var completed=await services.Store.ChangeAsync(request,false,CapacityLifecycleModel.Apply,receipt=>request.Complete(receipt),token);
        return await completed.NotifyAsync(services.Projector,services.Logger);
    }
    public static CapacityLifecycleCompletedEvent Complete(this ChangeCapacityReservationCommand request,CapacityLifecycleReceipt receipt)=>new()
    {
        Id=receipt.CompletedEventId,Subject=new(ActorType.Event,ChangeCapacityReservationCommand.Actor,nameof(CapacityLifecycleCompletedEvent),request.EntityId.Format()),
        EntityId=request.EntityId,CommandId=request.CommandId,OperationId=request.OperationId,PortfolioId=request.PortfolioId,
        CorrelationId=request.CorrelationId,CausationId=request.CausationId,CommittedAtUtc=receipt.CommittedAtUtc,ReceivedOn=receipt.CommittedAtUtc,
        InputHash=request.InputSha256,AggregateId=request.EntityId.Format(),Receipt=receipt
    };
}
