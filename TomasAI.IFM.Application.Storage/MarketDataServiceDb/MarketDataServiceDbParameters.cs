using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

internal enum DurableStoreWriteStage
{
    CurrentIntent,
    OperationResult,
    Outbox,
    AuthorityWatermark,
    LeaseIdentity
}

internal sealed record DurableCurrentRow(string Json, long Revision);

internal sealed record DurableOperationRow(
    string Digest,
    TomasAI.IFM.Application.MarketData.Subscriptions.Persistence.DurableIntentResult Result);

internal sealed record DurableExistsRow(bool Exists);

internal readonly record struct DurableSubscriptionParameters(NpgsqlParameter[] Items) : IBindValue
{
    public object Bind() => Items;
}

internal sealed class MarketDataServiceTransactionRepository(
    IDbConnectionSetting connection,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<MarketDataServiceTransactionRepository>(connection, logger)
{
    public override IObjectRepository Database => this;
}

internal readonly record struct CompositionRoutePlanIdParameter(string PlanId) : IBindValue
{
    public object Bind() => Values(Text(PlanId));
}

internal readonly record struct CompositionRoutePlanParameter(CompositionRoutePlan Plan, string Payload) : IBindValue
{
    public object Bind() => Values(Text(Plan.PlanId), Text(Payload));
}

internal readonly record struct RoleParameter(DatabentoContractRole Role) : IBindValue
{
    public object Bind() => Values(Text(Role.ToString()));
}

internal readonly record struct DeleteParameter(DatabentoContractRole Role, long Version) : IBindValue
{
    public object Bind() => Values(Text(Role.ToString()), Bigint(Version));
}

internal readonly record struct IdParameter(long Id) : IBindValue
{
    public object Bind() => Values(Bigint(Id));
}

internal readonly record struct IdentityParameter(Guid Id) : IBindValue
{
    public object Bind() => Values(Uuid(Id));
}

internal readonly record struct ObservationDeleteParameter(long Id, long Version) : IBindValue
{
    public object Bind() => Values(Bigint(Id), Bigint(Version));
}

internal readonly record struct AssignmentParameter(FuturesRolloverContractAssignment Assignment, long ExpectedRowVersion) : IBindValue
{
    public object Bind() => Assignment.BindAssignment(ExpectedRowVersion);
}

internal readonly record struct VxPairDeleteParameter(
    DatabentoContractRole FrontRole,
    long FrontExpected,
    DatabentoContractRole SecondRole,
    long SecondExpected) : IBindValue
{
    public object Bind() => Values(
        Text(FrontRole.ToString()),
        Bigint(FrontExpected),
        Text(SecondRole.ToString()),
        Bigint(SecondExpected));
}

internal readonly record struct VxPairParameter(
    FuturesRolloverContractAssignment Front,
    long FrontExpected,
    FuturesRolloverContractAssignment Second,
    long SecondExpected) : IBindValue
{
    public object Bind() => Values([.. Front.BindAssignment(FrontExpected), .. Second.BindAssignment(SecondExpected)]);
}

internal readonly record struct ObservationParameter(DatabentoWatchdogObservation Observation) : IBindValue
{
    public object Bind() => Values(
        Bigint(Observation.WatchdogStatusLogId),
        Uuid(Observation.ObservationId),
        Uuid(Observation.CorrelationId),
        Date(Observation.ValueDate),
        TimestampTz(Observation.ObservedOnUtc),
        Text(Observation.OperationReason.ToString()),
        Text(Observation.MajorStatus.ToString()),
        Text(Observation.DisplayHealth.ToString()),
        Boolean(Observation.CoreContractsReady),
        Integer(Observation.RecoveryAttempt),
        Text(Observation.NativeBackend),
        Integer(Observation.NativeAbiVersion),
        Uuid(Observation.NativeGeneration),
        Text(Observation.FailureStage),
        Text(Observation.FailureDetail),
        Text(JsonSerializer.Serialize(Observation.FeedStatusDetails)),
        Text("DatabentoMarketDataWatchdogService"));
}

internal readonly record struct ObservationListParameter(DateOnly? ValueDate, DatabentoMajorStatus? Status, int PageSize) : IBindValue
{
    public object Bind() => Values(Date(ValueDate), Text(Status?.ToString()), Integer(PageSize));
}

internal readonly record struct ObservationUpdateParameter(
    DatabentoWatchdogObservation Observation,
    long ExpectedRowVersion,
    string ChangedBy) : IBindValue
{
    public object Bind() => Values(
        Bigint(Observation.WatchdogStatusLogId),
        Uuid(Observation.ObservationId),
        Uuid(Observation.CorrelationId),
        Date(Observation.ValueDate),
        TimestampTz(Observation.ObservedOnUtc),
        Text(Observation.OperationReason.ToString()),
        Text(Observation.MajorStatus.ToString()),
        Text(Observation.DisplayHealth.ToString()),
        Boolean(Observation.CoreContractsReady),
        Integer(Observation.RecoveryAttempt),
        Text(Observation.NativeBackend),
        Integer(Observation.NativeAbiVersion),
        Uuid(Observation.NativeGeneration),
        Text(Observation.FailureStage),
        Text(Observation.FailureDetail),
        Text(JsonSerializer.Serialize(Observation.FeedStatusDetails)),
        Bigint(ExpectedRowVersion),
        Text(ChangedBy));
}

internal readonly record struct IncidentParameter(DatasetIncidentTransition Transition) : IBindValue
{
    public object Bind() => Values(
        Uuid(Transition.TransitionId),
        Uuid(Transition.Snapshot.IncidentId),
        Uuid(Transition.CorrelationId),
        Text(Transition.Snapshot.Dataset),
        Date(Transition.Snapshot.ValueDate),
        TimestampTz(Transition.Snapshot.ObservedOnUtc),
        Boolean(Transition.Snapshot.IsOpen),
        Text(JsonSerializer.Serialize(Transition.Snapshot)));
}
