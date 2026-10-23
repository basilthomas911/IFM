using MessagePack;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Development-only fault targeting one exact owned Options process generation.</summary>
[MessagePackObject]
public sealed record MarketDataDevelopmentFaultRequest(
    [property: Key(0)] Guid OperationId,
    [property: Key(1)] Guid WorkerInstanceId,
    [property: Key(2)] Guid GenerationId,
    [property: Key(3)] DateOnly ValueDate);
