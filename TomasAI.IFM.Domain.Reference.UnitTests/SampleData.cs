using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.JobScheduler;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Domain.Reference.UnitTests;

public static class SampleData
{
    static readonly DateTime _lookupTypeCreatedOn = new DateTime(2025, 01, 01, 0, 0, 0);
    static readonly DateTime _jobScheduledDate = new DateTime(2025, 01, 01, 0, 0, 0);
    static readonly DateTime _jobCreatedOn = new DateTime(2025, 01, 01, 0, 0, 0);
    static readonly DateTime _jobUpdatedOn = new DateTime(2025, 01, 01, 0, 0, 0);
    static readonly DateTime _economicEventDate = new DateTime(2025, 02, 15, 14, 30, 0);
    static readonly DateTime _economicCalendarCreatedOn = new DateTime(2025, 01, 01, 0, 0, 0);
    static readonly DateTime _mdiCreatedOn = new DateTime(2025, 01, 01, 0, 0, 0);
    
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

    public static readonly EconomicCalendarReadModel EconomicCalendar = new EconomicCalendarReadModel(
        eventDate: _economicEventDate,
        countryCode: "US",
        eventName: "Non-Farm Payrolls",
        actual: "250K",
        forecast: "240K",
        prior: "230K",
        createdOn: _economicCalendarCreatedOn,
        createdBy: "admin");

    public static readonly DefaultFuturesContractDefinitionsReadModel DefaultFuturesContractDefinitions = new DefaultFuturesContractDefinitionsReadModel
    {
        Currency = "USD",
        Exchange = "CME",
        Multiplier = "50",
        SecurityType = "FUT",
        OptionSecurityType = "FOP",
        Symbol = "ES"
    };

    public static readonly FuturesOptionStrikePriceReadModel FuturesOptionStrikePriceDefinitions = new FuturesOptionStrikePriceReadModel
    {
        Minimum = 4000,
        Maximum = 6000,
        Increment = 25
    };

    public static readonly MDIForwardLossRatioReadModel MDIForwardLossRatio = new MDIForwardLossRatioReadModel(
        mdi: 70,
        trendDirection: IntrinsicTimeTrendType.UpTrend,
        tradeType: TradeType.LongIronCondor,
        forwardLossRatio: 0.35,
        createdBy: "admin",
        createdOn: _mdiCreatedOn,
        updatedBy: "admin",
        updatedOn: _mdiCreatedOn
    );

    public static readonly MDIForwardLossRatioReadModel[] MDIForwardLossRatios = new[]
    {
        new MDIForwardLossRatioReadModel(
            mdi: 70,
            trendDirection: IntrinsicTimeTrendType.UpTrend,
            tradeType: TradeType.LongIronCondor,
            forwardLossRatio: 0.35,
            createdBy: "admin",
            createdOn: _mdiCreatedOn,
            updatedBy: "admin",
            updatedOn: _mdiCreatedOn
        ),
        new MDIForwardLossRatioReadModel(
            mdi: 80,
            trendDirection: IntrinsicTimeTrendType.UpTrend,
            tradeType: TradeType.LongIronCondor,
            forwardLossRatio: 0.25,
            createdBy: "admin",
            createdOn: _mdiCreatedOn,
            updatedBy: "admin",
            updatedOn: _mdiCreatedOn
        )
    };
}
