using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Durable internal emulator admission. Same-identity retries observe one broker order even after acknowledgement loss.</summary>
public sealed class EmulatorExecutionStore(IPostgresEventTransaction transactions, FinancialDevelopmentPolicy? developmentPolicy = null)
{
    /// <summary>Restores local emulator admission records without submitting or executing an order.</summary>
    public Task ReplayAsync(EmulatorOrderSubmittedEvent source,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.ProjectionRequest is not {} request || source.EventId<=0 || source.Id==Guid.Empty)
            throw new InvalidDataException("Committed emulator event with admission facts required.");
        return transactions.ExecuteAsync(async (db,ct)=>
        {
            await (db.ScalarAsync(PortfolioDbSql.Financial.LedgerConfigurationStore.Select01,[source.PortfolioId],ct)).ConfigureAwait(false);
            var revision=source.Receipt.FinancialRevision;
            if(!await (LedgerFinancialProjection.RecordAppliedAsync(db,source,revision,ct)).ConfigureAwait(false)) return false;
            var current=await (LockAuthorityAsync(db,source.PortfolioId,null,ct)).ConfigureAwait(false);
            if(revision!=current.Revision+1) throw new InvalidDataException("Financial emulator events must be rebuilt in revision order.");
            var order=source.Receipt.Order;
            await (db.ExecuteAsync(PortfolioDbSql.Financial.EmulatorExecutionStore.Insert01,
                [order.ExecutionId,source.PortfolioId,order.FundId,order.OrderId,order.ReservationId,source.OperationId,
                 LedgerFinancialProjection.PayloadJson(request),LedgerFinancialProjection.PayloadJson(source.Receipt),source.InputHash,source.CommittedAtUtc],ct)).ConfigureAwait(false);
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Insert06,
                [source.PortfolioId,source.OperationId,request.Subject.Name,source.InputHash,source.EventId,revision],ct)).ConfigureAwait(false);
            await (db.ExecuteAsync(PortfolioDbSql.Financial.PortfolioDbFinancialSupport.Update01,[source.PortfolioId,revision],ct)).ConfigureAwait(false);
            return true;
        },token);
    }

    public Task<EmulatorOrderSubmittedEvent> SubmitAsync(SubmitEmulatorOrderCommand request, CancellationToken token = default)
        => transactions.ExecuteAsync(async (db, ct) =>
    {
        var authority = await (LockAuthorityAsync(db, request.PortfolioId, null, ct, developmentPolicy)).ConfigureAwait(false);
        var replay = await (ReadOperationAsync<EmulatorOrderSubmittedEvent>(db, request.PortfolioId, request.OperationId, request.InputSha256, ct)).ConfigureAwait(false);
        if (replay is not null) return replay;
        var order = request.Body.Order; var now = DateTime.UtcNow;
        Require(string.Equals(request.InputSha256,FinancialCanonicalHash.Request(request),StringComparison.Ordinal) && string.Equals(order.ContentHash,order.Hash(),StringComparison.Ordinal) &&
            order.PortfolioId == request.PortfolioId && string.Equals(order.Environment,"Emulator",StringComparison.Ordinal) && string.Equals(authority.Book.Environment,"Emulator",StringComparison.Ordinal) &&
            string.Equals(authority.State,"Active",StringComparison.Ordinal) && now < order.ValidUntilUtc && now < request.ExpiresAtUtc,
            FinancialReasons.AuthorityDenied, "Current exact emulator order and financial authority are required.");
        await (ValidateFundSourcesAsync(db, authority.Book, order.FundId, true, ct)).ConfigureAwait(false);
        var existing = await (db.ScalarAsync(PortfolioDbSql.Financial.EmulatorExecutionStore.Select01, [order.ExecutionId], ct)).ConfigureAwait(false);
        if (existing is Guid prior) throw new FinancialOperationException(FinancialReasons.SourceConflict,
            "This execution already has an emulator order; reconcile its original operation.", FinancialCommitDisposition.NoNewMutation, prior);
        var rows = await (db.QueryAsync(PortfolioDbSql.Financial.EmulatorExecutionStore.Select02, [request.PortfolioId, order.ReservationId], r => (Status: (ReservationStatus)r.GetInt32(0), Version: r.GetInt64(1),
                Execution: r.IsDBNull(2) ? Guid.Empty : r.GetGuid(2), Request: Decode<CapacityReservationRequest>(r.GetString(3))), ct)).ConfigureAwait(false);
        Require(rows.Count == 1, FinancialReasons.AuthorityDenied, "Consumed reservation is required.");
        var row = rows[0]; var original = row.Request;
        var fund = authority.Book.Funds.Single(x => x.FundId == order.FundId);
        var reference = fund.Deployments.Length == 0 ? fund.Reference : fund.Deployments.SingleOrDefault(x => x.Reference.DeploymentKey == original.Authority.DeploymentKey)?.Reference;
        Require(row.Status == ReservationStatus.Consumed && row.Version == 2 && row.Execution == order.ExecutionId &&
            original.Authority == reference && original.FundId == order.FundId && original.BookId == order.BookId && original.OrderId == order.OrderId &&
            original.StrategyUnits == order.StrategyUnits && string.Equals(original.SizedOrderHash,order.SizedOrderHash,StringComparison.Ordinal) &&
            original.ValidUntilUtc == order.ValidUntilUtc && original.TradeIds.Order().SequenceEqual(order.Legs.Select(x => x.TradeId).Distinct().Order()),
            FinancialReasons.AuthorityRevoked, "The order no longer has its exact consumed capacity and Fund authority.");
        var consumed = await (ReadOperationAsync<CapacityConsumptionCompletedEvent>(db, request.PortfolioId, request.Body.ConsumptionOperationId,
            request.Body.ConsumptionInputHash, ct)).ConfigureAwait(false);
        Require(consumed?.Id == request.Body.ConsumptionCompletedEventId && consumed.Receipt.ReservationId == order.ReservationId &&
            consumed.Receipt.Status == ReservationStatus.Consumed, FinancialReasons.AuthorityDenied, "Committed consumption receipt is required.");
        var accepted = await (CapacityReservationStore.ReadEvidenceAsync<ICapacityExecutionAcceptedEvent>(db, order.ExecutionId, ct)).ConfigureAwait(false);
        Require(string.Equals(accepted?.CapacityAcceptance.ExecutionOrderHash,order.ContentHash,StringComparison.Ordinal) && accepted.CapacityAcceptance.ExecutionId == order.ExecutionId &&
            string.Equals(accepted.CapacityAcceptance.SizedOrderHash,order.SizedOrderHash,StringComparison.Ordinal), FinancialReasons.AuthorityDenied,
            "Only the exact order in the committed execution intent may be submitted.");
        var id = Guid.NewGuid(); var revision = checked(authority.Revision + 1);
        var receipt = new EmulatorOrderReceipt(order.ExecutionId, order, $"EMU-{order.ExecutionId:N}", revision, now, id);
        var completed = new EmulatorOrderSubmittedEvent
        {
            SchemaVersion = 2,
            ProjectionRequest = request,
            Id = id,
            CommandId = request.CommandId,
            OperationId = request.OperationId,
            PortfolioId = request.PortfolioId,
            EntityId = request.EntityId,
            Subject = new(ActorType.Event, SubmitEmulatorOrderCommand.Actor, nameof(EmulatorOrderSubmittedEvent), request.EntityId.Format()),
            CorrelationId = request.CorrelationId,
            CausationId = request.CausationId,
            InputHash = request.InputSha256,
            CommittedAtUtc = now,
            ReceivedOn = now,
            AggregateId = request.EntityId.Format(),
            Receipt = receipt
        };
        await (db.ExecuteAsync(PortfolioDbSql.Financial.EmulatorExecutionStore.Insert01, [order.ExecutionId,order.PortfolioId,order.FundId,order.OrderId,order.ReservationId,request.OperationId,
                LedgerFinancialProjection.PayloadJson(request),LedgerFinancialProjection.PayloadJson(receipt),request.InputSha256,now], ct)).ConfigureAwait(false);
        await (SaveOutcomeAsync(db, request, completed, revision, false, ct)).ConfigureAwait(false);
        await (LedgerFinancialProjection.RecordAppliedAsync(db,completed,revision,ct)).ConfigureAwait(false);
        return completed;
    }, token);
}
