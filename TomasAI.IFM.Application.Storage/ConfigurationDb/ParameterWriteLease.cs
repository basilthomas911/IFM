using Npgsql;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;
/// <summary>Owns and releases the PostgreSQL parameter writer lock transaction and connection.</summary>
internal sealed class ParameterWriteLease(NpgsqlConnection connection, NpgsqlTransaction transaction) : IAsyncDisposable
{
    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await (transaction.DisposeAsync())
            .ConfigureAwait(false);
        }
        finally
        {
            await (connection.DisposeAsync())
            .ConfigureAwait(false);
        }
    }
}
