namespace TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;

/// <summary>
/// Defines Option Pricer queries for direct, in-process use by domain actors.
/// </summary>
/// <remarks>Implementations must not use HTTP, NATS, or actor messaging.</remarks>
public interface IActorOptionPricerQueryApi : IOptionPricerQueryApi;
