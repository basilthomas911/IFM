namespace TomasAI.IFM.Framework.Storage;

public interface IBindValue
{
    /// <summary>
    /// Creates the provider bind payload. ScyllaDB catalogs return an <see cref="object"/> array ordered like the
    /// prepared statement's CQL markers. PostgreSQL catalogs return strongly typed, unnamed Npgsql parameters
    /// ordered like native <c>$n</c> placeholders. SQL Server bindings can return provider-specific name-bound
    /// payloads.
    /// </summary>
    object Bind();
}
