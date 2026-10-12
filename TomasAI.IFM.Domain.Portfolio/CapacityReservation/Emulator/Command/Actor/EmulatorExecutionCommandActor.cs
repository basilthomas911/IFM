using System.Collections.Frozen;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Emulator.Command;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Emulator.Command.Actor;

/// <summary>Maps financial Commands. Each handler loads and mutates authority within the database transaction.</summary>
public sealed class EmulatorExecutionCommandActor(ICommandActorContext<EmulatorExecutionCommandActor> context, EmulatorExecutionCommandServices services, ILogger<EmulatorExecutionCommandActor> logger)
    : BaseEventSourceCommandActor<EmulatorExecutionCommandActor>(context, logger)
{
    public const string ActorName = SubmitEmulatorOrderCommand.Actor;
    static readonly IReadOnlyDictionary<string, Func<IActorMessage, ICommand>> _parseMap = new Dictionary<string, Func<IActorMessage, ICommand>>(StringComparer.Ordinal)
    {
        [SubmitEmulatorOrderCommand.Verb] = message => message.AsCommand<SubmitEmulatorOrderCommand>()!,
    }.ToFrozenDictionary(StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>> _validationMap = new Dictionary<Type, Func<ICommand, List<ValidationError>>>
    {
        [typeof(SubmitEmulatorOrderCommand)] = static command =>
        {
            var typed = (SubmitEmulatorOrderCommand)command;
            return new List<ValidationError>()
                .ValidateCommandId(typed.CommandId, typed.CommandName)
                .ValidateFinancialEntityId(typed.EntityId, typed.CommandName)
                .ValidateEmulatorOrder(typed);
        },    }.ToFrozenDictionary();
    static readonly IReadOnlyDictionary<Type, Func<ICommand, EmulatorExecutionCommandServices, CancellationToken, ValueTask<ServiceResult<GuidResult>>>> _receiveMap =
        new Dictionary<Type, Func<ICommand, EmulatorExecutionCommandServices, CancellationToken, ValueTask<ServiceResult<GuidResult>>>>
        {
            [typeof(SubmitEmulatorOrderCommand)] = (command, owner, token) => ((SubmitEmulatorOrderCommand)command).ExecuteAsync(owner, token),
        }.ToFrozenDictionary();
    protected override ICommand ParseMessage(ICommandActorContext<EmulatorExecutionCommandActor> context, IActorMessage message) => ParseMappedCommand(context, message, _parseMap);
    protected override ValueTask OnValidateAsync(ICommandActorContext<EmulatorExecutionCommandActor> context, ActorThreadId threadId, ICommand command)
    {
        ValidateMappedCommand(command, _validationMap);
        return ValueTask.CompletedTask;
    }
    protected override ValueTask<IActorState> OnLoadStateAsync(ICommandActorContext<EmulatorExecutionCommandActor> context, ActorThreadId threadId, ICommand command)
        => command.LoadFinancialStateAsync();
    protected override ValueTask<bool> ShouldProcessDuplicateAsync(ICommandActorContext<EmulatorExecutionCommandActor> context, ICommand command, CancellationToken cancellationToken)
        => command.ReconcileFinancialDuplicateAsync(cancellationToken);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<EmulatorExecutionCommandActor> context, IActorState state, ICommand command)
        => ResolveMappedCommandHandler(command, _receiveMap)(command, services, CancellationToken.None);
    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(ICommandActorContext<EmulatorExecutionCommandActor> context, IActorState state, ICommand command, CancellationToken cancellationToken)
        => ResolveMappedCommandHandler(command, _receiveMap)(command, services, cancellationToken);
    protected override ValueTask OnStartup(ICommandActorContext<EmulatorExecutionCommandActor> context, CancellationToken cancellationToken) => services.Projector.StartAsync(context, cancellationToken);
    protected override ValueTask OnShutdown(ICommandActorContext<EmulatorExecutionCommandActor> context) => services.Projector.StopAsync();
    protected override ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(ICommandActorContext<EmulatorExecutionCommandActor> context, ActorThreadId threadId, ICommand command, Exception ex)
        => ex.FinancialCommandFailure(services.Logger);
}

