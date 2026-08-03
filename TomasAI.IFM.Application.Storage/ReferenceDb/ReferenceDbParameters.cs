using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

internal readonly record struct DeleteEconomicCalendar(DateTime eventDate, string countryCode, string eventName) : IBindValue
{
    public object Bind() => new object?[] { eventDate, countryCode, eventName };
}
internal readonly record struct DeleteLookupType(string lookupTypeName, int orderId) : IBindValue
{
    public object Bind() => new object?[] { lookupTypeName, orderId };
}
internal readonly record struct DeleteMDIForwardLossRatio(string trendDirection, string tradeType) : IBindValue
{
    public object Bind() => new object?[] { trendDirection, tradeType };
}
internal readonly record struct DeleteScheduledJob(int jobId) : IBindValue
{
    public object Bind() => new object?[] { jobId, jobId };
}
internal readonly record struct GetEconomicCalendarById(DateTime eventDate, string countryCode, string eventName) : IBindValue
{
    public object Bind() => new object?[] { eventDate, countryCode, eventName };
}
internal readonly record struct GetEconomicCalendars(DateTime startDate, DateTime endDate, string countryCode) : IBindValue
{
    public object Bind() => new object?[] { startDate, endDate, countryCode };
}
internal readonly record struct GetLookupType(string lookupTypeName) : IBindValue
{
    public object Bind() => new object?[] { lookupTypeName };
}
internal readonly record struct GetLookupTypeById(string lookupTypeName, int orderId) : IBindValue
{
    public object Bind() => new object?[] { lookupTypeName, orderId };
}
internal readonly record struct GetMDIForwardLossRatios(string trendDirection, string tradeType) : IBindValue
{
    public object Bind() => new object?[] { trendDirection, tradeType };
}
internal readonly record struct GetNextSeedId(string seedType) : IBindValue
{
    public object Bind() => new object?[] { seedType };
}
internal readonly record struct GetScheduledJobDays(int jobId) : IBindValue
{
    public object Bind() => new object?[] { jobId };
}
internal readonly record struct GetScheduledJobId(string jobName) : IBindValue
{
    public object Bind() => new object?[] { jobName };
}
internal readonly record struct InsertEconomicCalendar(DateTime eventDate, string countryCode, string eventName, string actual, string forecast, string prior, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { eventDate, countryCode, eventName, actual, forecast, prior, createdOn, createdBy };
}
internal readonly record struct InsertLookupType(string lookupTypeName, string shortCode, int orderId, string description, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { lookupTypeName, shortCode, orderId, description, createdOn, createdBy };
}
internal readonly record struct InsertMDIForwardLossRatio(int mdi, string trendDirection, string tradeType, double forwardLossRatio, string createdBy, DateTime? createdOn, string updatedBy, DateTime? updatedOn) : IBindValue
{
    public object Bind() => new object?[] { mdi, trendDirection, tradeType, forwardLossRatio, createdBy, createdOn, updatedBy, updatedOn };
}
internal readonly record struct InsertScheduledJob(int jobId, string jobName, string jobSchedule, DateTime jobScheduleDate, double jobScheduleInterval, string taskName, bool taskEnabled, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { jobId, jobName, jobSchedule, jobScheduleDate, jobScheduleInterval, taskName, taskEnabled, createdOn, createdBy, null, null };
}
internal readonly record struct InsertScheduledJobDays(int jobId, bool monday, bool tuesday, bool wednesday, bool thursday, bool friday, bool saturday, bool sunday) : IBindValue
{
    public object Bind() => new object?[] { jobId, monday, tuesday, wednesday, thursday, friday, saturday, sunday };
}
internal readonly record struct UpdateNextSeedId(string seedType) : IBindValue
{
    public object Bind() => new object?[] { seedType };
}
