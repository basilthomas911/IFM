using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Uses the ledger's Portfolio fence for admission, consumption and all lifecycle changes.</summary>
public sealed class CapacityReservationStore(IPostgresEventTransaction transactions, FinancialDevelopmentPolicy? developmentPolicy = null) : ICapacityReservationStore
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public Task<CapacityReservationCompletedEvent> ReserveAsync(ReservePortfolioTradeRiskCommand request,
        CapacityAdmissionCalculation validate, Func<CapacityReservationReceipt, CapacityReservationCompletedEvent> complete, CancellationToken token = default)
        
    {
        ArgumentNullException.ThrowIfNull(request);
        return CommitAsync(request, async (db, authority, cancellation) =>
    {
        using var trace = FinancialTelemetry.ActivitySource.StartActivity("financial.capacity.reserve");
        var body = request.Body; var now = DateTime.UtcNow;
        Require(string.Equals(authority.State,"Active",StringComparison.Ordinal) && body.BookId == authority.Book.BookId && string.Equals(body.ExecutionEnvironment,authority.Book.Environment,StringComparison.Ordinal),
            FinancialReasons.AuthorityDenied, "Financial book/environment is not active for this reservation.");
        await (ValidateFundSourcesAsync(db, authority.Book, body.FundId, true, cancellation)).ConfigureAwait(false);
        // This evidence is read from a committed Risk event, never accepted from the reserve caller.
        using var evidenceTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.capacity.read_risk_evidence");
        var result = await (ReadEvidenceAsync<ICapacityAssessmentCompletedEvent>(db, body.RiskInvocationId, cancellation)).ConfigureAwait(false);
        evidenceTrace?.Stop();
        Require(result is not null, FinancialReasons.AuthorityDenied, "Committed qualified Risk assessment is unavailable.");
        Require(body.Requirements.Exposures.Length is > 0 and <= 256, FinancialReasons.InvalidContract, "Capacity scope count exceeds its bound.");
        using var usageTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.capacity.read_usage");
        var used = await (db.QueryAsync(PortfolioDbSql.Financial.CapacityReservationStore.Select01, [request.PortfolioId,Json(body.Requirements.Exposures.Select(x=>new
                { scope_kind=(int)x.ScopeKind,scope_key=x.ScopeKey,measure=(int)x.Measure,unit=(int)x.Unit }).ToArray())], r => new CapacityUsed((CapacityScopeKind)r.GetInt32(0), r.GetString(1),
                (CapacityMeasure)r.GetInt32(2), (CapacityUnit)r.GetInt32(3), r.GetDecimal(4), r.GetDecimal(5), r.GetDecimal(6)), cancellation)).ConfigureAwait(false);
        usageTrace?.Stop();
        Require(used.Count <= 256, FinancialReasons.InvalidContract, "Capacity usage exceeds its bound.");
        using var cashTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.capacity.read_cash");
        var available = await (GeneralLedgerStore.AvailableCash(db, body.BookId, body.FundId, cancellation)).ConfigureAwait(false);
        cashTrace?.Stop();
        using (FinancialTelemetry.ActivitySource.StartActivity("financial.capacity.validate"))
            validate(body, result!.CapacityAssessment, authority.Book, available, used, now);
        using var existingTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.capacity.find_existing");
        var existing = await (db.ScalarAsync(PortfolioDbSql.Financial.CapacityReservationStore.Select02, [body.ReservationId, request.PortfolioId, body.OrderId], cancellation)).ConfigureAwait(false);
        existingTrace?.Stop();
        Require(existing is null, FinancialReasons.RequestMismatch, "Reservation identity/order already has a capacity decision.");
        var revision = checked(authority.Revision + 1);
        var receipt = new CapacityReservationReceipt
        {
            OperationId = request.OperationId,
            ReservationId = body.ReservationId,
            PortfolioId = request.PortfolioId,
            FundId = body.FundId,
            BookId = body.BookId,
            OrderId = body.OrderId,
            TradeIds = body.TradeIds,
            RiskResultId = body.RiskResultId,
            RiskAssessmentHash = body.RiskAssessmentHash,
            CompositionResultHash = body.CompositionResultHash,
            UnitCandidateHash = body.UnitCandidateHash,
            SizedOrderHash = body.SizedOrderHash,
            StrategyUnits = body.StrategyUnits,
            Requirements = body.Requirements,
            AuthorityEpoch = authority.Book.AuthorityEpoch,
            FinancialRevision = revision,
            GrantedAtUtc = now,
            ValidUntilUtc = body.ValidUntilUtc,
            ExecutionEnvironment = body.ExecutionEnvironment,
            CompletedEventId = Guid.NewGuid(),
            InputHash = request.InputSha256
        };
        using var writeTrace = FinancialTelemetry.ActivitySource.StartActivity("financial.capacity.write_reservation");
        await (db.ExecuteAsync(PortfolioDbSql.Financial.CapacityReservationStore.Insert01, [body.ReservationId,request.PortfolioId,request.OperationId,body.FundId,body.BookId,body.OrderId,
                body.UnitCandidateHash,body.RiskAssessmentHash,body.SizedOrderHash,body.StrategyUnits,Json(body.Requirements),
                body.ExecutionEnvironment,body.ValidUntilUtc,receipt.CompletedEventId,LedgerFinancialProjection.PayloadJson(body),LedgerFinancialProjection.PayloadJson(receipt),request.InputSha256], cancellation)).ConfigureAwait(false);
        writeTrace?.Stop();
        var usage = body.Requirements.Exposures.Select(x => CalculateUsage(x,(0,0,0),(1,0,0))).ToArray();
        using (FinancialTelemetry.ActivitySource.StartActivity("financial.capacity.write_usage"))
            foreach (var delta in usage)
                await (ApplyUsageAsync(db, request.PortfolioId, delta, revision, cancellation)).ConfigureAwait(false);
        var completed = complete(receipt) with { SchemaVersion=2, ProjectionFacts=new() { ActorName=request.Subject.Name,Request=body,Usage=usage } };
        CheckOutcome(request, completed, receipt.CompletedEventId);
        await (SaveOutcomeAsync(db, request, completed, revision, true, cancellation)).ConfigureAwait(false);
        await (LedgerFinancialProjection.RecordAppliedAsync(db,completed,revision,cancellation)).ConfigureAwait(false);
        return completed;
    }, token);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public Task<T> ChangeAsync<T>(IFinancialRequest<CapacityLifecycleRequest> request, bool consumption,
        CapacityLifecycleCalculation calculate, Func<CapacityLifecycleReceipt, T> complete, CancellationToken token = default)
        where T : class, IFinancialCompletedEvent 
    {
        ArgumentNullException.ThrowIfNull(request);
        return CommitAsync(request, async (db, authority, cancellation) =>
    {
        var body = request.Body; var now = DateTime.UtcNow;
        Require(consumption == (request is ConsumeCapacityReservationCommand) &&
            (!consumption || body.ChangeKind == CapacityChangeKind.Consume), FinancialReasons.InvalidLifecycle, "Consumption requires its dedicated Function contract.");
        var row = await (ReadReservationAsync(db, request.PortfolioId, body.ReservationId, cancellation)).ConfigureAwait(false);
        Require(row is not null, FinancialReasons.InvalidLifecycle, "Reservation does not belong to this Portfolio.");
        var (current, original) = row!.Value;
        Require(body.Source.SourceEventId != Guid.Empty && !string.IsNullOrWhiteSpace(body.Source.System) &&
            !string.IsNullOrWhiteSpace(body.Source.SourceContentHash), FinancialReasons.InvalidContract, "Lifecycle source identity/hash is required.");
        var source = $"{body.Source.System}:{body.Source.SourceEventId:N}";
        var prior = await (db.ScalarAsync(PortfolioDbSql.Financial.CapacityReservationStore.Select03,
            [body.ReservationId, source], cancellation)).ConfigureAwait(false);
        if (prior is Guid priorOperation) throw new FinancialOperationException(FinancialReasons.SourceConflict,
            "Lifecycle source has already been committed.", FinancialCommitDisposition.NoNewMutation, priorOperation);
        if (consumption)
        {
            Require(string.Equals(authority.State,"Active",StringComparison.Ordinal) && string.Equals(original.ExecutionEnvironment,authority.Book.Environment,StringComparison.Ordinal),
                FinancialReasons.AuthorityRevoked, "Financial authority no longer permits submission.");
            await (ValidateFundSourcesAsync(db, authority.Book, original.FundId, true, cancellation)).ConfigureAwait(false);
            var fund = authority.Book.Funds.Single(x => x.FundId == original.FundId);
            var reference = fund.Deployments.Length == 0 ? fund.Reference : fund.Deployments.SingleOrDefault(x => x.Reference.DeploymentKey == original.Authority.DeploymentKey)?.Reference;
            Require(original.Authority == reference,
                FinancialReasons.AuthorityRevoked, "Reservation authority changed before consumption.");
            var accepted = await (ReadEvidenceAsync<ICapacityExecutionAcceptedEvent>(db, body.ExecutionId, cancellation)).ConfigureAwait(false);
            var intent = accepted?.CapacityAcceptance;
            Require(intent is not null && intent.ExecutionId == body.ExecutionId && intent.ExecutionRevision == body.ExecutionRevision &&
                intent.ReservationId == body.ReservationId && intent.PortfolioId == request.PortfolioId && intent.FundId == original.FundId &&
                intent.OrderId == original.OrderId && string.Equals(intent.SizedOrderHash,original.SizedOrderHash,StringComparison.Ordinal) && string.Equals(intent.RequirementsHash,original.Requirements.ContentHash,StringComparison.Ordinal) &&
                string.Equals(intent.Environment,original.ExecutionEnvironment,StringComparison.Ordinal) && intent.ValidUntilUtc > now,
                FinancialReasons.AuthorityDenied, "Exact committed workflow acceptance is required before consumption.");
        }
        if (body.ChangeKind is CapacityChangeKind.ConfirmCancel or CapacityChangeKind.RecordPositionClose)
        {
            var reconciled = await (ReadEvidenceAsync<ICapacityExecutionReconciledEvent>(db, body.Source.SourceEventId, cancellation)).ConfigureAwait(false);
            var evidence = reconciled?.CapacityReconciliation;
            Require(evidence is not null && evidence.FinancialFactsComplete && evidence.ExecutionId == body.ExecutionId &&
                evidence.ExecutionRevision == body.ExecutionRevision && evidence.PortfolioId == request.PortfolioId && evidence.FundId == original.FundId &&
                evidence.OrderId == original.OrderId && evidence.ReservationId == body.ReservationId && string.Equals(evidence.SourceContentHash,body.Source.SourceContentHash,StringComparison.Ordinal) &&
                evidence.FilledUnits == body.FilledUnits && evidence.CancelledUnits == body.CancelledUnits && evidence.ClosedUnits == body.ClosedUnits && string.Equals(evidence.Environment,original.ExecutionEnvironment,StringComparison.Ordinal) &&
                string.Equals(evidence.ReconciliationReference,body.RelatedPostingReference,StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(body.RelatedPostingReference),
                FinancialReasons.InvalidLifecycle, "Exact committed terminal execution/financial reconciliation is required before release.");
        }
        var next = calculate(current, body, consumption, now); var revision = checked(authority.Revision + 1);
        var previous = Fractions(current);
        var usage = original.Requirements.Exposures.Select(x => CalculateUsage(x,previous,(next.HeldFraction,next.WorkingFraction,next.PositionFraction))).ToArray();
        foreach (var delta in usage) await (ApplyUsageAsync(db,request.PortfolioId,delta,revision,cancellation)).ConfigureAwait(false);
        var receipt = new CapacityLifecycleReceipt
        {
            OperationId = request.OperationId,
            ReservationId = body.ReservationId,
            ReservationVersion = checked(current.Version + 1),
            FinancialRevision = revision,
            Status = next.Status,
            FilledUnits = next.FilledUnits,
            CancelledUnits = next.CancelledUnits,
            RemainingUnits = next.RemainingUnits,
            ClosedUnits = next.ClosedUnits,
            CurrentRequirementsHash = original.Requirements.ContentHash,
            CommittedAtUtc = now,
            CompletedEventId = Guid.NewGuid(),
            InputHash = request.InputSha256
        };
        await (db.ExecuteAsync(PortfolioDbSql.Financial.CapacityReservationStore.Update01, [body.ReservationId,request.PortfolioId,(int)next.Status,receipt.ReservationVersion,
                body.ExecutionId==Guid.Empty?null:body.ExecutionId,next.FilledUnits,next.CancelledUnits,next.RemainingUnits,next.ClosedUnits], cancellation)).ConfigureAwait(false);
        await (db.ExecuteAsync(PortfolioDbSql.Financial.CapacityReservationStore.Insert02, [body.ReservationId,receipt.ReservationVersion,source,request.OperationId,(int)current.Status,(int)next.Status,Json(next),
                next.FilledUnits,next.CancelledUnits,next.RemainingUnits,body.ExecutionId==Guid.Empty?null:body.ExecutionId,
                body.ExecutionRevision,now,receipt.CompletedEventId,request.InputSha256,LedgerFinancialProjection.PayloadJson(receipt)], cancellation)).ConfigureAwait(false);
        var facts = new CapacityLifecycleProjectionFacts { ActorName=request.Subject.Name,Request=body,PreviousStatus=current.Status,Transition=next,Usage=usage };
        var completed = (T)(IFinancialCompletedEvent)(complete(receipt) switch
        {
            CapacityConsumptionCompletedEvent value => value with { SchemaVersion=2,ProjectionFacts=facts },
            CapacityLifecycleCompletedEvent value => value with { SchemaVersion=2,ProjectionFacts=facts },
            _ => throw new InvalidOperationException("Unsupported capacity completion contract.")
        });
        CheckOutcome(request, completed, receipt.CompletedEventId);
        await (SaveOutcomeAsync(db, request, completed, revision, consumption, cancellation)).ConfigureAwait(false);
        await (LedgerFinancialProjection.RecordAppliedAsync(db,completed,revision,cancellation)).ConfigureAwait(false);
        return completed;
    }, token);
    }

    public Task<ReservationSnapshot?> ReadCurrentAsync(int portfolioId, Guid reservationId, CancellationToken token = default)
        => transactions.ExecuteAsync(async (db, cancellation) => (await (ReadReservationAsync(db, portfolioId, reservationId, cancellation)).ConfigureAwait(false))?.State, token);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1075", Justification = "A failed receipt probe does not establish that COMMIT failed; preserve the original ambiguous outcome for reconciliation.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Retain the existing bounded persistence failure boundary: complete cleanup or report failure while preserving ambiguous commit/recovery evidence and the original cause.")]
    async Task<T> CommitAsync<T>(IFinancialRequest request,
        Func<EnlistedEventTransaction, (FinancialBookConfiguration Book, long Revision, string State), CancellationToken, Task<T>> apply,
        CancellationToken token) where T : class, IFinancialCompletedEvent
    {
        try
        {
            return await (transactions.ExecuteAsync(async (db, cancellation) =>
        {
            var replay = await (ReadOperationAsync<T>(db, request.PortfolioId, request.OperationId, request.InputSha256, cancellation)).ConfigureAwait(false);
            if (replay is not null) return replay;
            var authority = await (LockAuthorityAsync(db, request.PortfolioId, null, cancellation, developmentPolicy)).ConfigureAwait(false);
            replay = await (ReadOperationAsync<T>(db, request.PortfolioId, request.OperationId, request.InputSha256, cancellation)).ConfigureAwait(false);
            if (replay is not null) return replay;
            Require(request.ExpectedFinancialRevision == authority.Revision, FinancialReasons.RevisionConflict, "Financial state changed since preparation.");
            Require(request.ExpiresAtUtc > DateTime.UtcNow, FinancialReasons.TimeExpired, "Request expired before acquiring financial authority.");
            return await (apply(db, authority, cancellation)).ConfigureAwait(false);
        }, token)).ConfigureAwait(false);
        }
        catch (FunctionCommitOutcomeUnknownException)
        {
            using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            try
            {
                var replay = await (new PortfolioFinancialStore(transactions).ReadOperationAsync<T>(request.PortfolioId, request.OperationId, request.InputSha256, recovery.Token)).ConfigureAwait(false);
                if (replay is not null) return replay;
            }
            catch (Exception) { /* An unavailable receipt is not proof that COMMIT failed. */ }
            throw;
        }
    }

    static async Task<(ReservationSnapshot State, CapacityReservationRequest Request)?> ReadReservationAsync(
        EnlistedEventTransaction db, int portfolioId, Guid reservationId, CancellationToken token)
    {
        var rows = await (db.QueryAsync(PortfolioDbSql.Financial.CapacityReservationStore.Select04, [portfolioId, reservationId], r => (new ReservationSnapshot(reservationId, r.GetInt64(0), (ReservationStatus)r.GetInt32(1),
                r.GetInt32(2), r.GetInt32(3), r.GetInt32(4), r.GetInt32(5), r.GetString(6), r.GetDateTime(7), r.IsDBNull(8) ? null : r.GetGuid(8), r.GetInt64(9), r.GetInt32(11)),
                Decode<CapacityReservationRequest>(r.GetString(10))), token)).ConfigureAwait(false);
        return rows.Count == 0 ? null : rows.Single();
    }

    internal static async Task<T?> ReadEvidenceAsync<T>(EnlistedEventTransaction db, Guid invocationId, CancellationToken token) where T : class, IEvent
    {
        var events = await (db.QueryAsync(PortfolioDbSql.Financial.CapacityReservationStore.Select05, [invocationId], r => new EventLogReadModel(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetInt64(3),
                r.GetFieldValue<byte[]>(4), r.GetGuid(5), r.GetString(6), r.GetInt64(7)), token)).ConfigureAwait(false);
        Require(events.Count <= 64, FinancialReasons.AuthorityDenied, "Upstream evidence exceeds its bounded event count.");
        var matches = events.Select(x => x.ToDomainEvent()).OfType<T>().Take(2).ToArray();
        Require(matches.Length <= 1, FinancialReasons.AuthorityDenied, "Conflicting committed upstream evidence.");
        return matches.SingleOrDefault();
    }

    internal static AcceptedCapacityUsage CalculateUsage(CapacityExposure exposure,
        (decimal Held, decimal Working, decimal Position) previous,
        (decimal Held, decimal Working, decimal Position) next)
    {
        if (exposure.Measure == CapacityMeasure.PositionSlots && exposure.Unit == CapacityUnit.Positions)
        {
            // One partly filled strategy still occupies a whole position slot after its unfilled remainder is cancelled.
            previous = PositionSlot(previous);
            next = PositionSlot(next);
        }
        return new(exposure.ScopeKind,exposure.ScopeKey,exposure.Measure,exposure.Unit,
            Contribution(exposure.Amount,next.Held)-Contribution(exposure.Amount,previous.Held),
            Contribution(exposure.Amount,next.Working)-Contribution(exposure.Amount,previous.Working),
            Contribution(exposure.Amount,next.Position)-Contribution(exposure.Amount,previous.Position));
    }

    internal static Task<int> ApplyUsageAsync(EnlistedEventTransaction db,int portfolioId,AcceptedCapacityUsage delta,long revision,CancellationToken token)
        => db.ExecuteAsync(PortfolioDbSql.Financial.CapacityReservationStore.Insert03,
            [portfolioId,(int)delta.ScopeKind,delta.ScopeKey,(int)delta.Measure,(int)delta.Unit,delta.Held,delta.Working,delta.Position,revision],token);

    static (decimal Held, decimal Working, decimal Position) PositionSlot((decimal Held, decimal Working, decimal Position) value)
        {
        if (value.Position > 0) return (0, 0, 1);
        if (value.Working > 0) return (0, 1, 0);
        return value.Held > 0 ? (1, 0, 0) : (0, 0, 0);
    }

    // Quantize each absolute contribution, then subtract. Rounding each incremental fill independently leaves residual holds.
    static decimal Contribution(decimal amount, decimal fraction) => decimal.Round(Math.Abs(amount) * fraction, 10, MidpointRounding.AwayFromZero);

    static (decimal Held, decimal Working, decimal Position) Fractions(ReservationSnapshot state) =>
        (state.Status == ReservationStatus.Reserved ? (decimal)state.RemainingUnits / state.StrategyUnits : 0,
         state.Status is not (ReservationStatus.Reserved or ReservationStatus.Released or ReservationStatus.Expired or ReservationStatus.Filled)
            ? (decimal)state.RemainingUnits / state.StrategyUnits : 0, (decimal)(state.FilledUnits - state.ClosedUnits) / state.StrategyUnits);

    static void CheckOutcome(IFinancialRequest request, IFinancialCompletedEvent completed, Guid eventId) =>
        Require(completed.Id == eventId && completed.OperationId == request.OperationId && completed.PortfolioId == request.PortfolioId &&
            string.Equals(completed.InputHash,request.InputSha256,StringComparison.Ordinal), FinancialReasons.InvalidContract, "Completion must describe this exact committed operation.");
}
