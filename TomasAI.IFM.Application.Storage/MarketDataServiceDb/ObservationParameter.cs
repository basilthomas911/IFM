using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using static TomasAI.IFM.Framework.Storage.Postgres.PostgresParameter;

namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
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
