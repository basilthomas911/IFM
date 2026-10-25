using TomasAI.IFM.Shared.MarketData.Diagnostics;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

[MessagePackObject]
public sealed record DatasetWorkerRecordProgress(
    [property: Key(0)] string Dataset, [property: Key(1)] string ContractId, [property: Key(2)] string RecordKind,
    [property: Key(3)] ushort PublisherId, [property: Key(4)] uint InstrumentId,
    [property: Key(5)] uint SourceSequence, [property: Key(6)] DateTimeOffset StartedAtUtc)
{
    public void Validate()
    {
        if (Dataset is null || Dataset.Length > 64 || ContractId is null || ContractId.Length > 256
            || RecordKind is null || RecordKind.Length > 64)
            throw new InvalidDataException("Worker in-flight record diagnostic bounds are invalid.");
    }
    public static DatasetWorkerRecordProgress From(TickAggregationRecordProgress value) 
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(
        DatasetWorkerDiagnostics.Bound(value.Dataset, 64), DatasetWorkerDiagnostics.Bound(value.ContractId, 256),
        DatasetWorkerDiagnostics.Bound(value.RecordKind, 64), value.PublisherId, value.InstrumentId,
        value.SourceSequence, value.StartedAtUtc);
    }
    public TickAggregationRecordProgress ToProgress() => new(Dataset, ContractId, RecordKind,
        PublisherId, InstrumentId, SourceSequence, StartedAtUtc);
}
