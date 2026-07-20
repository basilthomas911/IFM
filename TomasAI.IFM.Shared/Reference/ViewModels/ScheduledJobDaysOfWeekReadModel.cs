using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Reference.ViewModels;

/// <summary>
/// MessagePack-serializable view model describing the days of week a scheduled job should run.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys and
/// a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ScheduledJobDaysOfWeekReadModel
{
    /// <summary>Unique identifier of the scheduled job.</summary>
    [Key(0)]
    public int JobId { get; init; }

    /// <summary>True if the job runs on Monday.</summary>
    [Key(1)]
    public bool Monday { get; init; }

    /// <summary>True if the job runs on Tuesday.</summary>
    [Key(2)]
    public bool Tuesday { get; init; }

    /// <summary>True if the job runs on Wednesday.</summary>
    [Key(3)]
    public bool Wednesday { get; init; }

    /// <summary>True if the job runs on Thursday.</summary>
    [Key(4)]
    public bool Thursday { get; init; }

    /// <summary>True if the job runs on Friday.</summary>
    [Key(5)]
    public bool Friday { get; init; }

    /// <summary>True if the job runs on Saturday.</summary>
    [Key(6)]
    public bool Saturday { get; init; }

    /// <summary>True if the job runs on Sunday.</summary>
    [Key(7)]
    public bool Sunday { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public ScheduledJobDaysOfWeekReadModel() { }

    /// <summary>
    /// Creates a new scheduled job days-of-week configuration.
    /// </summary>
    public ScheduledJobDaysOfWeekReadModel(
        int jobId,
        bool monday,
        bool tuesday,
        bool wednesday,
        bool thursday,
        bool friday,
        bool saturday,
        bool sunday)
    {
        JobId = jobId;
        Monday = monday;
        Tuesday = tuesday;
        Wednesday = wednesday;
        Thursday = thursday;
        Friday = friday;
        Saturday = saturday;
        Sunday = sunday;
    }
}