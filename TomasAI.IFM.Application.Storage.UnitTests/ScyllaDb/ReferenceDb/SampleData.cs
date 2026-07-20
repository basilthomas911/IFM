using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.JobScheduler;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Application.Storage.UnitTests.ScyllaDb.ReferenceDb;


public static class SampleData
{
    static readonly DateTime _lookupTypeCreatedOn = new DateTime(2025, 01, 01, 0, 0, 0);
    static readonly DateTime _jobScheduledDate = new DateTime(2025, 01, 01, 0, 0, 0);
    static readonly DateTime _jobCreatedOn = new DateTime(2025, 01, 01, 0, 0, 0);
    static readonly DateTime _jobUpdatedOn = new DateTime(2025, 01, 01, 0, 0, 0);
    static SampleData()
    {

    }

    public static readonly LookupTypeReadModel LookupType = new LookupTypeReadModel(
       lookupTypeName: "SampleType",
       shortCode: "ST",
       orderId: 1,
       description: "This is a sample lookup type",
       createdOn: _lookupTypeCreatedOn,
       createdBy: "admin");

    public static readonly ScheduledJobReadModel ScheduledJob = new ScheduledJobReadModel(
        jobId: 1,
        jobName: "DailyBackup",
        jobSchedule: JobScheduleType.Daily,
        jobScheduleDate: _jobScheduledDate,
        jobScheduleInterval: 24.0,
        taskName: "BackupTask",
        taskEnabled: true,
        createdOn: _jobCreatedOn,
        createdBy: "admin",
        updatedOn: _jobUpdatedOn,
        updatedBy: "admin"
    )
    {
        DaysOfWeek = new ScheduledJobDaysOfWeekReadModel
        (
            jobId: 1,
            monday: true,
            tuesday: true,
            wednesday: true,
            thursday: true,
            friday: true,
            saturday: false,
            sunday: false
        )
    };

}
