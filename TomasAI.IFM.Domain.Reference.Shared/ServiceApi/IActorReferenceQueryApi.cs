namespace TomasAI.IFM.Domain.Reference.Shared.ServiceApi;

/// <summary>
/// Defines Reference queries for direct, in-process use by domain actors.
/// </summary>
/// <remarks>Implementations must not use HTTP, NATS, or actor messaging.</remarks>
public interface IActorReferenceQueryApi : IReferenceQueryApi;
