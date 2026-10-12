namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Provides a registry for actor types that can be used within the application.
/// </summary>
/// <remarks>This interface allows retrieval of all registered actor types. It is typically used in scenarios 
/// where dynamic discovery or management of actor types is required.</remarks>
public interface IActorRegistry
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized/configuration array contract and consumer bindings; changing collection types is a separate API migration.")]
    Type[] ActorTypes { get; }
}
