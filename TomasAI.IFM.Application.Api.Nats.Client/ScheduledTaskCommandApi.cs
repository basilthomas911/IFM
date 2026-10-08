using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Application.Api.Nats.Client;
/// <summary>Sends concrete scheduled-task commands using the established actor request/reply transport.</summary>
public sealed class ScheduledTaskCommandApi(IActorProducer producer) : IScheduledTaskCommandApi
{
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> CreateScheduledTaskAsync(CreateScheduledTaskCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, CreateScheduledTaskCommand.Actor, CreateScheduledTaskCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<CreateScheduledTaskCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> ChangeScheduledTaskScheduleAsync(ChangeScheduledTaskScheduleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, ChangeScheduledTaskScheduleCommand.Actor, ChangeScheduledTaskScheduleCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<ChangeScheduledTaskScheduleCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> EnableScheduledTaskAsync(EnableScheduledTaskCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, EnableScheduledTaskCommand.Actor, EnableScheduledTaskCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<EnableScheduledTaskCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> DisableScheduledTaskAsync(DisableScheduledTaskCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, DisableScheduledTaskCommand.Actor, DisableScheduledTaskCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<DisableScheduledTaskCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> RemoveScheduledTaskAsync(RemoveScheduledTaskCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, RemoveScheduledTaskCommand.Actor, RemoveScheduledTaskCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<RemoveScheduledTaskCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskInstallationAsync(RecordScheduledTaskInstallationCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, RecordScheduledTaskInstallationCommand.Actor, RecordScheduledTaskInstallationCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<RecordScheduledTaskInstallationCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskInstallationFailureAsync(RecordScheduledTaskInstallationFailureCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, RecordScheduledTaskInstallationFailureCommand.Actor, RecordScheduledTaskInstallationFailureCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<RecordScheduledTaskInstallationFailureCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> AdmitScheduledTaskRunAsync(AdmitScheduledTaskRunCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, AdmitScheduledTaskRunCommand.Actor, AdmitScheduledTaskRunCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<AdmitScheduledTaskRunCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskRunCompletionAsync(RecordScheduledTaskRunCompletionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, RecordScheduledTaskRunCompletionCommand.Actor, RecordScheduledTaskRunCompletionCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<RecordScheduledTaskRunCompletionCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> RegisterScheduledTaskProjectAsync(RegisterScheduledTaskProjectCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, RegisterScheduledTaskProjectCommand.Actor, RegisterScheduledTaskProjectCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<RegisterScheduledTaskProjectCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskHostCapabilityAsync(RecordScheduledTaskHostCapabilityCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, RecordScheduledTaskHostCapabilityCommand.Actor, RecordScheduledTaskHostCapabilityCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<RecordScheduledTaskHostCapabilityCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> RequestScheduledTaskRunAsync(RequestScheduledTaskRunCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, RequestScheduledTaskRunCommand.Actor, RequestScheduledTaskRunCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<RequestScheduledTaskRunCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskRunAdmissionAsync(RecordScheduledTaskRunAdmissionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, RecordScheduledTaskRunAdmissionCommand.Actor, RecordScheduledTaskRunAdmissionCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<RecordScheduledTaskRunAdmissionCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskRunStartedAsync(RecordScheduledTaskRunStartedCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, RecordScheduledTaskRunStartedCommand.Actor, RecordScheduledTaskRunStartedCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<RecordScheduledTaskRunStartedCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> CompleteScheduledTaskRunAsync(CompleteScheduledTaskRunCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, CompleteScheduledTaskRunCommand.Actor, CompleteScheduledTaskRunCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<CompleteScheduledTaskRunCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> FailScheduledTaskRunAsync(FailScheduledTaskRunCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, FailScheduledTaskRunCommand.Actor, FailScheduledTaskRunCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<FailScheduledTaskRunCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskRunUncertainAsync(RecordScheduledTaskRunUncertainCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, RecordScheduledTaskRunUncertainCommand.Actor, RecordScheduledTaskRunUncertainCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<RecordScheduledTaskRunUncertainCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
    /// <inheritdoc />
    public ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskRunStageAsync(RecordScheduledTaskRunStageCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var normalized = command with { Subject = new(ActorType.Command, RecordScheduledTaskRunStageCommand.Actor, RecordScheduledTaskRunStageCommand.Verb, command.EntityId.Format()) };
        return producer.RequestAsync<RecordScheduledTaskRunStageCommand, ScheduledTaskId, GuidResult>(normalized.Subject, normalized, normalized.EntityId, cancellationToken);
    }
}
