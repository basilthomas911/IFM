namespace TomasAI.IFM.Shared.JobScheduler
{

    public static class JobScheduleTypeExtensions
    {
        public static string ToStringFast(this JobScheduleType value) => value switch
        {
            JobScheduleType.Daily => nameof(JobScheduleType.Daily),
            _ => value.ToString()
        };
    }
}
