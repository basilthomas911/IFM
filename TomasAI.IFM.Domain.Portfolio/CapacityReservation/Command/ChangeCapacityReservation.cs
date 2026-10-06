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

public sealed record CapacityReservationCommandServices(ICapacityReservationStore Store, IPortfolioDbReadContext Database,
    IEventProjector<CapacityReservationCommandActor> Projector, ILogger<CapacityReservationCommandActor> Logger);

public static class ChangeCapacityReservation
{
    /// <summary>Handles Execute Async at the command boundary.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="services">The enlisted persistence, receipt replay and projection services.</param>
    /// <param name="cancellationToken">Cancels pre-commit work; an uncertain commit must be reconciled.</param>
    /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
    /// <exception cref="OperationCanceledException">Cancellation interrupts pre-commit work; committed or uncertain operations require reconciliation.</exception>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this ChangeCapacityReservationCommand command, CapacityReservationCommandServices services, CancellationToken cancellationToken)
    {
        var replay = await services.Database.ReadOperationAsync<CapacityLifecycleCompletedEvent>(command.PortfolioId, command.OperationId, command.InputSha256, cancellationToken);
        if (replay is not null) return await replay.NotifyAsync(services.Projector, services.Logger);
        FinancialRequestValidation.Demand(command, "CapacityLifecycle", DateTime.UtcNow);
        var completed = await services.Store.ChangeAsync(command, false, command.Compute, receipt => command.Complete(receipt), cancellationToken);
        return await completed.NotifyAsync(services.Projector, services.Logger);
    }
    /// <summary>Handles Complete at the command boundary.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="receipt">The verified committed financial receipt.</param>
    /// <returns>The operation result.</returns>
    public static CapacityLifecycleCompletedEvent Complete(this ChangeCapacityReservationCommand command, CapacityLifecycleReceipt receipt) => new()
    {
        Id = receipt.CompletedEventId,
        Subject = new(ActorType.Event, ChangeCapacityReservationCommand.Actor, nameof(CapacityLifecycleCompletedEvent), command.EntityId.Format()),
        EntityId = command.EntityId,
        CommandId = command.CommandId,
        OperationId = command.OperationId,
        PortfolioId = command.PortfolioId,
        CorrelationId = command.CorrelationId,
        CausationId = command.CausationId,
        CommittedAtUtc = receipt.CommittedAtUtc,
        ReceivedOn = receipt.CommittedAtUtc,
        InputHash = command.InputSha256,
        AggregateId = command.EntityId.Format(),
        Receipt = receipt
    };
    /// <summary>Calculates a capacity transition from the reservation read inside the financial fence.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="reservation">The reservation business input.</param>
    /// <param name="capacityChange">The capacity change business input.</param>
    /// <param name="consumptionFunction">The consumption function business input.</param>
    /// <param name="nowUtc">The authoritative UTC decision time.</param>
    /// <returns>The computed business values or created event; no transport or storage effects.</returns>
    /// <exception cref="InvalidOperationException">The current business state cannot accept the requested transition.</exception>
    internal static CapacityTransition Compute(this ChangeCapacityReservationCommand command,
        ReservationSnapshot reservation, CapacityLifecycleRequest capacityChange, bool consumptionFunction, DateTime nowUtc)
        => CapacityLifecycleModel.Apply(reservation, capacityChange, consumptionFunction, nowUtc);
}
