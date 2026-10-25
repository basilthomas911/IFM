namespace TomasAI.IFM.Shared.JobScheduler
{
    public interface IScheduledJobTaskResolver
    {
        IScheduledJobTask[] Resolve();
    }
}
