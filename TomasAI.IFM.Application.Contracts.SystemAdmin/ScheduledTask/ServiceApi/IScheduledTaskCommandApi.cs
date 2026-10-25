using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Queries;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;
/// <summary>Exposes typed actor requests for task configuration and execution receipts.</summary>
public interface IScheduledTaskCommandApi
{
    /// <summary>Requests CreateScheduledTask and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> CreateScheduledTaskAsync(CreateScheduledTaskCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests ChangeScheduledTaskSchedule and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> ChangeScheduledTaskScheduleAsync(ChangeScheduledTaskScheduleCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests EnableScheduledTask and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> EnableScheduledTaskAsync(EnableScheduledTaskCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests DisableScheduledTask and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> DisableScheduledTaskAsync(DisableScheduledTaskCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests RemoveScheduledTask and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> RemoveScheduledTaskAsync(RemoveScheduledTaskCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests RecordScheduledTaskInstallation and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskInstallationAsync(RecordScheduledTaskInstallationCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests RecordScheduledTaskInstallationFailure and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskInstallationFailureAsync(RecordScheduledTaskInstallationFailureCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests AdmitScheduledTaskRun and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> AdmitScheduledTaskRunAsync(AdmitScheduledTaskRunCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests RecordScheduledTaskRunCompletion and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskRunCompletionAsync(RecordScheduledTaskRunCompletionCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests RegisterScheduledTaskProject and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> RegisterScheduledTaskProjectAsync(RegisterScheduledTaskProjectCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests RecordScheduledTaskHostCapability and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskHostCapabilityAsync(RecordScheduledTaskHostCapabilityCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests RequestScheduledTaskRun and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> RequestScheduledTaskRunAsync(RequestScheduledTaskRunCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests RecordScheduledTaskRunAdmission and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskRunAdmissionAsync(RecordScheduledTaskRunAdmissionCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests RecordScheduledTaskRunStarted and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskRunStartedAsync(RecordScheduledTaskRunStartedCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests CompleteScheduledTaskRun and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> CompleteScheduledTaskRunAsync(CompleteScheduledTaskRunCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests FailScheduledTaskRun and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> FailScheduledTaskRunAsync(FailScheduledTaskRunCommand command, CancellationToken cancellationToken = default);
    /// <summary>Requests RecordScheduledTaskRunUncertain and waits for source-state acceptance.</summary>
    ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskRunUncertainAsync(RecordScheduledTaskRunUncertainCommand command, CancellationToken cancellationToken = default);
    /// <summary>Records a confirmed business stage in the running occurrence source stream.</summary>
    ValueTask<ServiceResult<GuidResult>> RecordScheduledTaskRunStageAsync(RecordScheduledTaskRunStageCommand command, CancellationToken cancellationToken = default);
}
