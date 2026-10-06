using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Emulator.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Emulator.Command;

public sealed record EmulatorExecutionCommandServices(EmulatorExecutionStore Store, IPortfolioDbReadContext Database,
    IEventProjector<EmulatorExecutionCommandActor> Projector, ILogger<EmulatorExecutionCommandActor> Logger);

/// <summary>Mapped emulator command handling; replay uses its original receipt before checking the original deadline.</summary>
public static class SubmitEmulatorOrder
{
    /// <summary>Handles Validate Emulator Order at the command boundary.</summary>
    /// <param name="errors">The accumulated ingress validation failures.</param>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <returns>The operation result.</returns>
    public static List<ValidationError> ValidateEmulatorOrder(this List<ValidationError> errors, SubmitEmulatorOrderCommand command)
    {
        errors.ValidateFinancialRequest<SubmitEmulatorOrderCommand, SubmitEmulatorOrderRequest>(command,
            TomasAI.IFM.Shared.EventModelActor.ActorType.Command, SubmitEmulatorOrderCommand.Actor, SubmitEmulatorOrderCommand.Verb);
        var submission = command.Body; var emulatorOrder = submission?.Order;
        if (emulatorOrder is null || submission!.ConsumptionOperationId == Guid.Empty || submission.ConsumptionCompletedEventId == Guid.Empty ||
            string.IsNullOrWhiteSpace(submission.ConsumptionInputHash) || emulatorOrder.ExecutionId == Guid.Empty || emulatorOrder.ReservationId == Guid.Empty ||
            emulatorOrder.PortfolioId != command.PortfolioId || emulatorOrder.FundId <= 0 || emulatorOrder.BookId <= 0 || emulatorOrder.OrderId <= 0 ||
            emulatorOrder.StrategyUnits <= 0 || emulatorOrder.Environment != "Emulator" || emulatorOrder.Currency != "USD" || emulatorOrder.EntryFees < 0 ||
            emulatorOrder.Legs is not { Length: >= 1 and <= 16 } || emulatorOrder.Legs.Any(x => x is null || x.TradeId <= 0 ||
                string.IsNullOrWhiteSpace(x.InstrumentId) || x.Side is not ("Buy" or "Sell") || x.Contracts <= 0 || x.Multiplier != 50) ||
            emulatorOrder.ContentHash != emulatorOrder.Hash())
            errors.Add(new("Exact consumed ES emulator emulatorOrder is required."));
        return errors;
    }

    /// <summary>Handles Execute Async at the command boundary.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="services">The enlisted persistence, receipt replay and projection services.</param>
    /// <param name="cancellationToken">Cancels pre-commit work; an uncertain commit must be reconciled.</param>
    /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
    /// <exception cref="OperationCanceledException">Cancellation interrupts pre-commit work; committed or uncertain operations require reconciliation.</exception>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this SubmitEmulatorOrderCommand command,
        EmulatorExecutionCommandServices services, CancellationToken cancellationToken)
    {
        var replay = await services.Database.ReadOperationAsync<EmulatorOrderSubmittedEvent>(command.PortfolioId, command.OperationId, command.InputSha256, cancellationToken);
        if (replay is not null) return await replay.NotifyAsync(services.Projector, services.Logger);
        FinancialRequestValidation.Demand(command, "EmulatorSubmit", DateTime.UtcNow);
        var completed = await services.Store.SubmitAsync(command, cancellationToken);
        return await completed.NotifyAsync(services.Projector, services.Logger);
    }
}
