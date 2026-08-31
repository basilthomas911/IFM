using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

internal readonly record struct GetTradeStrategyFamilies(string catalog) : IBindValue { public object Bind() => new object?[] { catalog }; }
internal readonly record struct InsertTradeStrategyFamily(string catalog, int tradeStrategyFamilyId, long definitionVersion, string systemKey, string name, string state, DateTime createdOnUtc, string createdBy) : IBindValue
{ public object Bind() => new object?[] { catalog, tradeStrategyFamilyId, definitionVersion, systemKey, name, state, createdOnUtc, createdBy }; }

internal readonly record struct DeleteReferenceProjectionStateV3(string projectionName) : IBindValue
{
    public object Bind() => new object?[] { projectionName };
}
internal readonly record struct DeleteReferenceProjectionMutationV3(string projectionName, Guid mutationId) : IBindValue
{
    public object Bind() => new object?[] { projectionName, mutationId };
}
internal readonly record struct DeleteReferenceProjectionMutationsV3(string projectionName) : IBindValue
{
    public object Bind() => new object?[] { projectionName };
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
    public object Bind() => new object?[] { jobId };
}
internal readonly record struct DeleteScheduledJobDays(int jobId) : IBindValue
{
    public object Bind() => new object?[] { jobId };
}
internal readonly record struct DeleteScheduledJobByNameV3ForOfflineRepair(string jobName) : IBindValue
{
    public object Bind() => new object?[] { jobName };
}
internal readonly record struct ReleaseScheduledJobNameV3(
    string jobName,
    int jobId,
    Guid reservationToken) : IBindValue
{
    public object Bind() => new object?[] { jobName, jobId, reservationToken };
}
internal readonly record struct ReleaseScheduledJobWriteOwnershipV3(
    string scopeType,
    string scopeKey,
    Guid operationId) : IBindValue
{
    public object Bind() => new object?[] { scopeType, scopeKey, operationId };
}
internal readonly record struct GetReferenceProjectionStateV3(string projectionName) : IBindValue
{
    public object Bind() => new object?[] { projectionName };
}
internal readonly record struct GetReferenceProjectionMutationsV3(string projectionName) : IBindValue
{
    public object Bind() => new object?[] { projectionName };
}
internal readonly record struct InvalidateReferenceProjectionStateV3(Guid generation, string projectionName) : IBindValue
{
    public object Bind() => new object?[] { generation, projectionName };
}
internal readonly record struct CompleteReferenceProjectionStateV3(DateTime completedOn, string projectionName, Guid generation) : IBindValue
{
    public object Bind() => new object?[] { completedOn, projectionName, generation };
}
internal readonly record struct InsertReferenceProjectionMutationV3(string projectionName, Guid mutationId, DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { projectionName, mutationId, startedOn };
}
internal readonly record struct ClaimReferenceProjectionOwnershipV3(string projectionName, Guid mutationId, DateTime claimedOn) : IBindValue
{
    public object Bind() => new object?[] { projectionName, mutationId, claimedOn };
}
internal readonly record struct FlagReferenceProjectionOwnershipConflictV3(string projectionName) : IBindValue
{
    public object Bind() => new object?[] { projectionName };
}
internal readonly record struct ReleaseReferenceProjectionOwnershipV3(string projectionName, Guid mutationId) : IBindValue
{
    public object Bind() => new object?[] { projectionName, mutationId };
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
internal readonly record struct GetScheduledJobDays(int jobId) : IBindValue
{
    public object Bind() => new object?[] { jobId };
}
internal readonly record struct GetScheduledJob(int jobId) : IBindValue
{
    public object Bind() => new object?[] { jobId };
}
internal readonly record struct GetScheduledJobId(string jobName) : IBindValue
{
    public object Bind() => new object?[] { jobName };
}
internal readonly record struct GetScheduledJobReservationV3(string jobName) : IBindValue
{
    public object Bind() => new object?[] { jobName };
}
internal readonly record struct GetScheduledJobWriteOwnershipV3(
    string scopeType,
    string scopeKey) : IBindValue
{
    public object Bind() => new object?[] { scopeType, scopeKey };
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
internal readonly record struct InsertScheduledJobByNameV3(
    string jobName,
    int jobId,
    Guid reservationToken) : IBindValue
{
    public object Bind() => new object?[] { jobName, jobId, reservationToken };
}
internal readonly record struct ClaimScheduledJobWriteOwnershipV3(
    string scopeType,
    string scopeKey,
    Guid operationId,
    DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { scopeType, scopeKey, operationId, startedOn };
}
internal readonly record struct RotateScheduledJobNameV3Reservation(
    Guid reservationToken,
    string jobName,
    int jobId,
    Guid expectedReservationToken) : IBindValue
{
    public object Bind() => new object?[]
    {
        reservationToken,
        jobName,
        jobId,
        expectedReservationToken
    };
}
internal readonly record struct InsertScheduledJobDays(int jobId, bool monday, bool tuesday, bool wednesday, bool thursday, bool friday, bool saturday, bool sunday) : IBindValue
{
    public object Bind() => new object?[] { jobId, monday, tuesday, wednesday, thursday, friday, saturday, sunday };
}
