using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.JobScheduler;

public interface IScheduledJobDaysOfWeek
{
    int JobId { get; }
    bool Monday { get; }
    bool Tuesday { get; }
    bool Wednesday { get; }
    bool Thursday { get; }
    bool Friday { get; }
    bool Saturday { get; }
    bool Sunday { get; }

    ScheduledJobDaysOfWeekReadModel ToViewModel();
}
