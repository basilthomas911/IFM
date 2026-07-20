using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.JobScheduler;

namespace TomasAI.IFM.Service.JobScheduler
{
    public class ScheduledJobsResolver : IScheduledJobTaskResolver
    {
        private Func<IScheduledJobTask[]> _resolverFunction;

        /// <summary>
        /// create event handler resolver
        /// </summary>
        /// <param name="resolverFunction">function that will return query handler using dependancy injection</param>
        public ScheduledJobsResolver(Func<IScheduledJobTask[]> resolverFunction)
            => _resolverFunction = resolverFunction;

       
        /// <summary>
        /// return array of scheduled jobs
        /// </summary>
        /// <returns></returns>
        public IScheduledJobTask[] Resolve()
        {
            var scheduledJobTasks = default(IScheduledJobTask[]);
            try
            {
                scheduledJobTasks = _resolverFunction();
            }
            catch { }
            return scheduledJobTasks;
        }
    }
}
