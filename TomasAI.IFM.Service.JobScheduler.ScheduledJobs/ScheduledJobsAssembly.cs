using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace TomasAI.IFM.Service.JobScheduler.ScheduledJobs
{
    public static class ScheduledJobsAssembly
    {
        public static Assembly Current => Assembly.GetExecutingAssembly();
    }

}
