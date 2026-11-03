namespace TomasAI.IFM.Shared.JobScheduler
{

    public static class JobScheduleTypeExtensions
    {

        extension(JobScheduleType value)
        {
            public string ToStringFast() => value switch
            {
                JobScheduleType.Daily => nameof(JobScheduleType.Daily),
                _ => value.ToString()
            };
        }
    }
}
