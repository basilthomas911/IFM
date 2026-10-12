using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using System.Security.Cryptography;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Immutable first-writer-wins accounting requests, retained for execution replay.</summary>
public sealed class BrokerAccountingIntentStore(IPostgresEventTransaction transactions)
{
    public Task<PostFundTransactionsCommand?> ReadAsync(int portfolioId, Guid operationId, string evidenceHash,
        CancellationToken token = default) => transactions.ExecuteAsync(
            (db, ct) => ReadAsync(db, portfolioId, operationId, evidenceHash, ct), token);

    public Task<PostFundTransactionsCommand> ClaimAsync(PostFundTransactionsCommand proposed, string evidenceHash,
        CancellationToken token = default) => transactions.ExecuteAsync(
            (db, ct) => ClaimEnlistedAsync(db, proposed, evidenceHash, ct), token);

    /// <summary>Claims the immutable request in the caller's allocation transaction.</summary>
    public static async Task<PostFundTransactionsCommand> ClaimEnlistedAsync(EnlistedEventTransaction db,
        PostFundTransactionsCommand proposed, string evidenceHash, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(proposed);
        // Serialize claims for this execution before checking legacy receipts/audit records.
        await (db.ScalarAsync("SELECT pg_advisory_xact_lock(hashtextextended($1, 0));",
            [$"broker-accounting:{proposed.PortfolioId}:{proposed.OperationId:N}"], ct)).ConfigureAwait(false);
        var existing = await (ReadAsync(db, proposed.PortfolioId, proposed.OperationId, evidenceHash, ct)).ConfigureAwait(false);
        if (existing is not null) return existing;
        var legacy = await (db.ScalarAsync("""
            SELECT EXISTS(SELECT 1 FROM command_log WHERE commandid=$1)
                OR EXISTS(SELECT 1 FROM portfolio_financial.financial_operation_receipt
                    WHERE portfolio_id=$2 AND operation_id=$3);
            """, [proposed.CommandId, proposed.PortfolioId, proposed.OperationId], ct)).ConfigureAwait(false);
        Require(legacy is not true, FinancialReasons.RequestMismatch,
            "Existing accounting operation has no immutable intent; reconciliation is required.");
        Require(string.Equals(proposed.InputSha256,FinancialCanonicalHash.Request(proposed),StringComparison.Ordinal),
            FinancialReasons.RequestMismatch, "Accounting intent hash is invalid.");
        var payload = new CommandAuditMessagePackCodec().Serialize(proposed);
        await (db.ExecuteAsync("""
            INSERT INTO portfolio_financial.broker_accounting_intent
                (portfolio_id,operation_id,evidence_hash,command_payload,command_hash)
            VALUES($1,$2,$3,$4::jsonb,$5);
            """, [proposed.PortfolioId, proposed.OperationId, evidenceHash, Json(payload),
                Convert.ToHexString(payload.Sha256)], ct)).ConfigureAwait(false);
        var source=new BrokerAccountingIntentCapturedEvent { Id=Guid.NewGuid(),CommandId=proposed.CommandId,Subject=proposed.Subject,
            AggregateId=proposed.OperationId.ToString("N"),ReceivedOn=DateTime.UtcNow,PortfolioId=proposed.PortfolioId,OperationId=proposed.OperationId,
            EvidenceHash=evidenceHash,PayloadFormat=(short)payload.Format,PayloadVersion=payload.Version,CommandBytes=payload.Bytes,CommandSha256=payload.Sha256 };
        await (FinancialOperationalEventPersistence.AppendAsync(db,$"BrokerAccountingIntent.{proposed.PortfolioId}.{proposed.OperationId:N}",source,proposed.PortfolioId,ct)).ConfigureAwait(false);
        return proposed;
    }

    /// <summary>Restores the exact captured transport without dispatching an accounting command.</summary>
    public Task ReplayAsync(BrokerAccountingIntentCapturedEvent source,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.SchemaVersion!=1 || source.EventId<=0 || source.Id==Guid.Empty) throw new InvalidDataException("Committed accounting intent facts required.");
        return transactions.ExecuteAsync(async(db,ct)=>
        {
            await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select01,[source.PortfolioId],ct)).ConfigureAwait(false);
            if(!await (FinancialOperationalEventPersistence.ClaimAsync(db,source,source.PortfolioId,ct)).ConfigureAwait(false)) return false;
            var payload=new CommandAuditPayload((CommandAuditPayloadFormat)source.PayloadFormat,source.PayloadVersion,source.CommandBytes,source.CommandSha256);
            await (db.ExecuteAsync("""
                INSERT INTO portfolio_financial.broker_accounting_intent(portfolio_id,operation_id,evidence_hash,command_payload,command_hash)
                VALUES($1,$2,$3,$4::jsonb,$5);
                """,[source.PortfolioId,source.OperationId,source.EvidenceHash,Json(payload),Convert.ToHexString(source.CommandSha256)],ct)).ConfigureAwait(false);
            return true;
        },token);
    }

    public static async Task<PostFundTransactionsCommand?> ReadAsync(EnlistedEventTransaction db,
        int portfolioId, Guid operationId, string evidenceHash, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(db);
        var rows = await (db.QueryAsync("""
            SELECT evidence_hash,command_payload::text,command_hash
            FROM portfolio_financial.broker_accounting_intent WHERE portfolio_id=$1 AND operation_id=$2;
            """, [portfolioId, operationId], r => (Evidence: r.GetString(0), Payload: r.GetString(1), Hash: r.GetString(2)), token)).ConfigureAwait(false);
        if (rows.Count == 0) return null;
        var row = rows.Single();
        Require(string.Equals(row.Evidence,evidenceHash,StringComparison.Ordinal), FinancialReasons.RequestMismatch,
            "Execution evidence differs from the original accounting intent.");
        var payload = Decode<CommandAuditPayload>(row.Payload);
        Require(payload.Bytes is { Length: > 0 } && payload.Sha256 is { Length: 32 } &&
            string.Equals(Convert.ToHexString(SHA256.HashData(payload.Bytes)),row.Hash,StringComparison.Ordinal) &&
            string.Equals(Convert.ToHexString(payload.Sha256),row.Hash,StringComparison.Ordinal),
            FinancialReasons.RequestMismatch, "Stored accounting transport failed integrity validation.");
        var codec = new CommandAuditMessagePackCodec();
        var command = (PostFundTransactionsCommand)codec.Deserialize(typeof(PostFundTransactionsCommand),
            payload.Bytes, (short)payload.Format, payload.Version);
        Require(command.PortfolioId == portfolioId && command.OperationId == operationId &&
            string.Equals(command.InputSha256,FinancialCanonicalHash.Request(command),StringComparison.Ordinal) &&
            codec.Matches(command, payload.Sha256),
            FinancialReasons.RequestMismatch, "Stored accounting intent failed integrity validation.");
        return command;
    }
}
