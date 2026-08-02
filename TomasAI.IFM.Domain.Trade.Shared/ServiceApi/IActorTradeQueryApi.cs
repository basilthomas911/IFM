namespace TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

/// <summary>
/// Defines Trade queries for direct, in-process use by domain actors.
/// </summary>
/// <remarks>Implementations must not use HTTP, NATS, or actor messaging.</remarks>
public interface IActorTradeQueryApi : ITradeQueryApi;
