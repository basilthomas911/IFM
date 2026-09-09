using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Emulator;

public sealed record EmulatorExecutionCommandServices(EmulatorExecutionStore Store,IPortfolioFinancialDbContext Database,
    IEventProjector<EmulatorExecutionCommandActor> Projector,ILogger<EmulatorExecutionCommandActor> Logger);

/// <summary>Mapped emulator command handling; replay uses its original receipt before checking the original deadline.</summary>
public static class SubmitEmulatorOrder
{
    public static List<ValidationError> ValidateEmulatorOrder(this List<ValidationError> errors,SubmitEmulatorOrderCommand request)
    {
        errors.ValidateFinancialRequest<SubmitEmulatorOrderCommand,SubmitEmulatorOrderRequest>(request,
            TomasAI.IFM.Shared.EventModelActor.ActorType.Command,SubmitEmulatorOrderCommand.Actor,SubmitEmulatorOrderCommand.Verb);
        var b=request.Body; var order=b?.Order;
        if(order is null || b!.ConsumptionOperationId==Guid.Empty || b.ConsumptionCompletedEventId==Guid.Empty ||
            string.IsNullOrWhiteSpace(b.ConsumptionInputHash) || order.ExecutionId==Guid.Empty || order.ReservationId==Guid.Empty ||
            order.PortfolioId!=request.PortfolioId || order.FundId<=0 || order.BookId<=0 || order.OrderId<=0 ||
            order.StrategyUnits<=0 || order.Environment!="Emulator" || order.Currency!="USD" || order.EntryFees<0 ||
            order.Legs is not { Length:>=1 and <=16 } || order.Legs.Any(x=>x is null || x.TradeId<=0 ||
                string.IsNullOrWhiteSpace(x.InstrumentId) || x.Side is not ("Buy" or "Sell") || x.Contracts<=0 || x.Multiplier!=50) ||
            order.ContentHash!=order.Hash())
            errors.Add(new("Exact consumed ES emulator order is required."));
        return errors;
    }

    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this SubmitEmulatorOrderCommand request,
        EmulatorExecutionCommandServices services,CancellationToken token)
    {
        var replay=await services.Database.ReadOperationAsync<EmulatorOrderSubmittedEvent>(request.PortfolioId,request.OperationId,request.InputSha256,token);
        if(replay is not null) return await replay.NotifyAsync(services.Projector,services.Logger);
        FinancialRequestValidation.Demand(request,"EmulatorSubmit",DateTime.UtcNow);
        var completed=await services.Store.SubmitAsync(request,token);
        return await completed.NotifyAsync(services.Projector,services.Logger);
    }
}
