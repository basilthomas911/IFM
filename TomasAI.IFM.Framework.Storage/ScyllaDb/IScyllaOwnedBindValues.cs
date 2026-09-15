namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

/// <summary>
/// Gives the Scylla binder exclusive, one-time ownership of a fresh positional parameter array.
/// Implementations must reject a second take so a resolved UDT value cannot be rebound on another session.
/// </summary>
public interface IScyllaOwnedBindValues
{
    /// <summary>Transfers the fresh positional values to one bind operation; a second call must fail.</summary>
    object?[] TakeValues();
}
