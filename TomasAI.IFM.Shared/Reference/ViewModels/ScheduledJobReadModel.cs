using System;
using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.JobScheduler;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Reference.ViewModels;

/// <summary>
/// MessagePack-serializable view model describing a scheduled job definition and its scheduling metadata.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys; derived members
/// are excluded from serialization using IgnoreMember/JsonIgnore.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ScheduledJobReadModel
{
    /// <summary>Unique identifier for the scheduled job.</summary>
    [Key(0)]
    public int JobId { get; init; }

    /// <summary>Human-readable job name.</summary>
    [Key(1)]
    public string JobName { get; init; }

    /// <summary>Schedule type (e.g., Daily).</summary>
    [Key(2)]
    public JobScheduleType JobSchedule { get; init; }

    /// <summary>Anchor date/time for the schedule (as-of/next-run reference).</summary>
    [Key(3)]
    public DateTime JobScheduleDate { get; init; }

    /// <summary>Schedule interval (units defined by JobSchedule).</summary>
    [Key(4)]
    public double JobScheduleInterval { get; init; }

    /// <summary>Fully-qualified task name or handler key.</summary>
    [Key(5)]
    public string TaskName { get; init; }

    /// <summary>Indicates whether this job is enabled.</summary>
    [Key(6)]
    public bool TaskEnabled { get; init; }

    /// <summary>Creation timestamp (UTC preferred).</summary>
    [Key(7)]
    public DateTime CreatedOn { get; init; }

    /// <summary>User or system that created the job.</summary>
    [Key(8)]
    public string CreatedBy { get; init; }

    /// <summary>Last updated timestamp (UTC preferred).</summary>
    [Key(9)]
    public DateTime UpdatedOn { get; init; }

    /// <summary>User or system that last updated the job.</summary>
    [Key(10)]
    public string UpdatedBy { get; init; }

    /// <summary>Optional day-of-week schedule details.</summary>
    [Key(11)]
    public ScheduledJobDaysOfWeekReadModel? DaysOfWeek { get; set; }

    /// <summary>
    /// Parameterless constructor for serializers; initializes strings to empty and timestamps to now.
    /// </summary>
    public ScheduledJobReadModel()
    {
        JobName = string.Empty;
        TaskName = string.Empty;
        CreatedBy = string.Empty;
        UpdatedBy = string.Empty;
        CreatedOn = DateTime.UtcNow;
        UpdatedOn = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a new scheduled job view model instance.
    /// </summary>
    public ScheduledJobReadModel(
        int jobId,
        string jobName,
        JobScheduleType jobSchedule,
        DateTime jobScheduleDate,
        double jobScheduleInterval,
        string taskName,
        bool taskEnabled,
        DateTime createdOn,
        string createdBy,
        DateTime updatedOn,
        string updatedBy)
    {
        JobId = jobId;
        JobName = jobName;
        JobSchedule = jobSchedule;
        JobScheduleDate = jobScheduleDate;
        JobScheduleInterval = jobScheduleInterval;
        TaskName = taskName;
        TaskEnabled = taskEnabled;
        CreatedOn = createdOn;
        CreatedBy = createdBy;
        UpdatedOn = updatedOn;
        UpdatedBy = updatedBy;
    }

    /// <summary>Derived composite identifier (excluded from MessagePack).</summary>
    [JsonIgnore]
    [IgnoreMember]
    public ScheduledJobId Id => new(JobId, JobName);

    [JsonIgnore]
    [IgnoreMember]
    public bool IsValid => JobId > 0 && !string.IsNullOrEmpty(JobName);

    /// <summary>
    /// Returns a compact JSON representation of the model.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);
}

/// <summary>
/// MessagePack-serializable identifier for a scheduled job, composed of JobId and JobName.
/// </summary>
/// <remarks>
/// Implements IActorEntityId. The formatted key uses dot notation: "JobId.JobName".
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ScheduledJobId(
    [property: Key(0)] int JobId,
    [property: Key(1)] string JobName) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public ScheduledJobId() : this(0, string.Empty) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string: "JobId.JobName".
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[64], $"{JobId}.{JobName}");

    /// <summary>
    /// Returns a human-readable string representation of the identifier.
    /// </summary>
    public override string ToString() => $"{JobId} : {JobName}";
}
