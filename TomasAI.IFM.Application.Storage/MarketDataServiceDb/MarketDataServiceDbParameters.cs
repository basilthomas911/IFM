using System.Text.Json;
using Npgsql;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

/// <summary>Ordered PostgreSQL bindings for AssignmentParameter.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct AssignmentParameter(FuturesRolloverContractAssignment Assignment, long ExpectedRowVersion) : IBindValue
{
    /// <summary>Gets the ordered values for the SQL operation.</summary>
    public object Bind() => Assignment.BindAssignment(ExpectedRowVersion);
}

/// <summary>Ordered PostgreSQL bindings for CompositionRoutePlanIdParameter.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CompositionRoutePlanIdParameter(string PlanId) : IBindValue
{
    /// <summary>Gets the ordered values for the SQL operation.</summary>
    public object Bind() => Values(Text(PlanId));
}

/// <summary>Ordered PostgreSQL bindings for CompositionRoutePlanParameter.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CompositionRoutePlanParameter(CompositionRoutePlan Plan, string Payload) : IBindValue
{
    /// <summary>Gets the ordered values for the SQL operation.</summary>
    public object Bind() => Values(Text(Plan.PlanId), Text(Payload));
}

/// <summary>Ordered PostgreSQL bindings for DeleteParameter.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteParameter(DatabentoContractRole Role, long Version) : IBindValue
{
    /// <summary>Gets the ordered values for the SQL operation.</summary>
    public object Bind() => Values(Text(Role.ToString()), Bigint(Version));
}

/// <summary>Ordered PostgreSQL bindings for DurableSubscriptionParameters.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DurableSubscriptionParameters(NpgsqlParameter[] Items) : IBindValue
{
    /// <summary>Gets the ordered values for the SQL operation.</summary>
    public object Bind() => Items;
}

/// <summary>Ordered PostgreSQL bindings for IdentityParameter.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct IdentityParameter(Guid Id) : IBindValue
{
    /// <summary>Gets the ordered values for the SQL operation.</summary>
    public object Bind() => Values(Uuid(Id));
}

/// <summary>Ordered PostgreSQL bindings for IdParameter.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct IdParameter(long Id) : IBindValue
{
    /// <summary>Gets the ordered values for the SQL operation.</summary>
    public object Bind() => Values(Bigint(Id));
}

/// <summary>Ordered PostgreSQL bindings for IncidentParameter.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct IncidentParameter(DatasetIncidentTransition Transition) : IBindValue
{
    /// <summary>Gets the ordered values for the SQL operation.</summary>
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

/// <summary>Ordered PostgreSQL bindings for ObservationDeleteParameter.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ObservationDeleteParameter(long Id, long Version) : IBindValue
{
    /// <summary>Gets the ordered values for the SQL operation.</summary>
    public object Bind() => Values(Bigint(Id), Bigint(Version));
}

/// <summary>Ordered PostgreSQL bindings for ObservationListParameter.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ObservationListParameter(DateOnly? ValueDate, DatabentoMajorStatus? Status, int PageSize) : IBindValue
{
    /// <summary>Gets the ordered values for the SQL operation.</summary>
    public object Bind() => Values(Date(ValueDate), Text(Status?.ToString()), Integer(PageSize));
}

/// <summary>Ordered PostgreSQL bindings for ObservationParameter.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ObservationParameter(DatabentoWatchdogObservation Observation) : IBindValue
{
    /// <summary>Gets the ordered values for the SQL operation.</summary>
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

/// <summary>Ordered PostgreSQL bindings for RoleParameter.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct RoleParameter(DatabentoContractRole Role) : IBindValue
{
    /// <summary>Gets the ordered values for the SQL operation.</summary>
    public object Bind() => Values(Text(Role.ToString()));
}

/// <summary>Ordered PostgreSQL bindings for ObservationUpdateParameter.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct ObservationUpdateParameter(
    DatabentoWatchdogObservation Observation,
    long ExpectedRowVersion,
    string ChangedBy) : IBindValue
{
    /// <summary>Gets the ordered values for the SQL operation.</summary>
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

/// <summary>Ordered PostgreSQL bindings for VxPairDeleteParameter.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct VxPairDeleteParameter(
    DatabentoContractRole FrontRole,
    long FrontExpected,
    DatabentoContractRole SecondRole,
    long SecondExpected) : IBindValue
{
    /// <summary>Gets the ordered values for the SQL operation.</summary>
    public object Bind() => Values(
        Text(FrontRole.ToString()),
        Bigint(FrontExpected),
        Text(SecondRole.ToString()),
        Bigint(SecondExpected));
}

/// <summary>Ordered PostgreSQL bindings for VxPairParameter.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct VxPairParameter(
    FuturesRolloverContractAssignment Front,
    long FrontExpected,
    FuturesRolloverContractAssignment Second,
    long SecondExpected) : IBindValue
{
    /// <summary>Gets the ordered values for the SQL operation.</summary>
    public object Bind() => Values([.. Front.BindAssignment(FrontExpected), .. Second.BindAssignment(SecondExpected)]);
}
