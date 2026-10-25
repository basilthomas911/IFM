namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>Resolves a persisted event identity without coupling storage to business assemblies.</summary>
public interface IEventTypeResolver
{
    /// <summary>Returns a concrete event type, or null when the identity is unavailable.</summary>
    Type? Resolve(string identity);
    /// <summary>Requires a known identity; preserves legacy CLR loading exceptions for existing durable callers.</summary>
    Type ResolveRequired(string identity) => EventTypeRegistry.RequireResolved(Resolve(identity), identity);
}
