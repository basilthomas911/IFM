using System.Diagnostics;
using TomasAI.IFM.Application.Storage.EventSourceDb.Diagnostics;
using TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;
using System.Data;
using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;


using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>Shared financial Command/Function unit of work. No transaction is stored in a singleton context.</summary>
public sealed class PostgresEventTransaction(
    IDbConnectionSettings settings,
    EventLogPersistenceOptions? eventLogPersistenceOptions = null) : IPostgresEventTransaction
{
    readonly string connectionString = ResolveConnectionString(settings);
    readonly EventLogMessagePackCodec eventLogCodec = new(
        (eventLogPersistenceOptions ?? new EventLogPersistenceOptions()).Validate().UseLz4Compression);
    readonly EventLogSqlLayout eventLogSqlLayout = EventLogSqlLayout.ForProduction(
        (eventLogPersistenceOptions ?? new EventLogPersistenceOptions()).Validate());



    public static void ValidatePortfolioDatabaseIdentity(IDbConnectionSettings settings) =>
        PostgresDatabaseIdentity.RequireSamePhysicalDatabase(settings,
            EventSourceActorDbContext.EventSourceActorDbConnection,
            PortfolioDb.PortfolioDbContext.PortfolioDbConnection);

    static string ResolveConnectionString(IDbConnectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings[PortfolioDb.PortfolioDbContext.PortfolioDbConnection] is not null)
            ValidatePortfolioDatabaseIdentity(settings);
        return settings[EventSourceActorDbContext.EventSourceActorDbConnection].ConnectionString;
    }

    /// <inheritdoc />
    public Task<T> ExecuteOnceAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return ExecuteAttemptAsync(operation, cancellationToken);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1994", Justification = "The retry/date loop is terminated by explicit success, cancellation, or bounded-history exits in its body.")]
    public async Task<T> ExecuteAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        for (var attempt = 0; ; attempt++)
        {
            using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.transaction");
            trace?.SetTag("financial.transaction.attempt", attempt + 1);
            var started = Stopwatch.GetTimestamp();
            try
            {
                var result = await ExecuteAttemptAsync(operation, cancellationToken).ConfigureAwait(false);
                trace?.SetTag("financial.transaction.outcome", "committed");
                FinancialTelemetry.Transaction(Stopwatch.GetElapsedTime(started).TotalMilliseconds, "committed"); return result;
            }
            catch (PostgresException error) when (attempt < 2 && error.SqlState is PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure)
            {
                // Only a confirmed server rollback may replay this database-only delegate.
                // COMMIT uncertainty, uniqueness conflicts, lock timeouts and application refusals never retry here.
                FinancialTelemetry.Transaction(Stopwatch.GetElapsedTime(started).TotalMilliseconds, "rolled_back_retry");
                trace?.SetTag("financial.transaction.outcome", "rolled_back_retry");
                FinancialTelemetry.Retry();
                await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                var outcome = error switch
                {
                    FunctionCommitOutcomeUnknownException => "unknown",
                    OperationCanceledException => "cancelled",
                    PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable } => "lock_timeout",
                    PostgresException { SqlState: PostgresErrorCodes.QueryCanceled } => "statement_timeout",
                    _ => "failed"
                };
                trace?.SetTag("financial.transaction.outcome", outcome);
                FinancialTelemetry.Transaction(Stopwatch.GetElapsedTime(started).TotalMilliseconds, outcome); throw;
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Retain the existing bounded persistence failure boundary: complete cleanup or report failure while preserving ambiguous commit/recovery evidence and the original cause.")]
    async Task<T> ExecuteAttemptAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        using (FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.open_connection"))
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var beginTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.begin");
        var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        await using var transactionDisposal = transaction.ConfigureAwait(false);
        beginTrace?.Stop();
        var enlisted = new EnlistedEventTransaction(connection, transaction, eventLogCodec, eventLogSqlLayout);
        T result;
        try
        {
            // Limit lock/provider waits; caller cancellation can impose a stricter operation deadline.
            using (FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.configure"))
                await enlisted.ExecuteAsync("SET LOCAL lock_timeout = '1000ms'; SET LOCAL statement_timeout = '2000ms';", [], cancellationToken).ConfigureAwait(false);
            using (FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.apply"))
                result = await operation(enlisted, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch
        {
            // COMMIT has not been sent. Disposal closes a connection if rollback itself cannot be confirmed.
            try
            {
                using var rollbackTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.rollback");
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch { /* Preserve the original database failure; cleanup is best effort. */ }
            throw;
        }
        finally
        {
            enlisted.Complete();
        }
        try
        {
            using var commitTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.transaction.commit");
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException)
        {
            // A server error response (for example deferred constraint failure) confirms COMMIT failed.
            throw;
        }
        catch (Exception exception)
        {
            throw new FunctionCommitOutcomeUnknownException(
                "PostgreSQL COMMIT was not acknowledged. Reconcile the original operation receipt before retrying.", exception);
        }
        return result; // Cancellation after confirmed commit cannot turn success into rollback.
    }
}
