using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;

/// <summary>Pure lifecycle rules; receipts never substitute for rechecking current state.</summary>
public static class CapacityLifecycleModel
{
    public static CapacityTransition Apply(ReservationSnapshot current, CapacityLifecycleRequest change,
        bool consumptionFunction, DateTime nowUtc)
    {
        Require(current.ReservationId == change.ReservationId && current.Version == change.ExpectedReservationVersion,
            FinancialReasons.RevisionConflict, "Reservation identity/version changed.");
        Require(current.RequirementsHash == change.ExpectedRequirementsHash,
            FinancialReasons.RequestMismatch, "Reservation requirements do not match.");
        Require(current.StrategyUnits > 0 && change.FilledUnits >= current.FilledUnits &&
            change.CancelledUnits >= current.CancelledUnits && change.RemainingUnits >= 0 &&
            (long)change.FilledUnits + change.CancelledUnits + change.RemainingUnits == current.StrategyUnits,
            FinancialReasons.InvalidLifecycle, "Filled, cancelled and remaining units must conserve the approved quantity.");
        Require(change.ClosedUnits >= current.ClosedUnits && change.ClosedUnits <= change.FilledUnits &&
            (change.ChangeKind == CapacityChangeKind.RecordPositionClose || change.ClosedUnits == current.ClosedUnits),
            FinancialReasons.InvalidLifecycle, "Only a reconciled position close may reduce open filled exposure.");
        Require(consumptionFunction == (change.ChangeKind == CapacityChangeKind.Consume),
            FinancialReasons.InvalidLifecycle, "Consumption requires its dedicated Function; lifecycle Commands cannot consume.");
        Require(current.Status is not (ReservationStatus.Undefined or ReservationStatus.Released or ReservationStatus.Expired) &&
            (current.Status != ReservationStatus.Filled || change.ChangeKind == CapacityChangeKind.RecordPositionClose),
            FinancialReasons.InvalidLifecycle, "A terminal reservation cannot transition again.");
        if (current.ExecutionId is { } execution)
            Require(execution == change.ExecutionId && change.ExecutionRevision >= current.ExecutionRevision,
                FinancialReasons.RequestMismatch, "Execution identity/revision does not match the committed order.");
        var unchanged = change.FilledUnits == current.FilledUnits && change.CancelledUnits == current.CancelledUnits && change.RemainingUnits == current.RemainingUnits;
        ReservationStatus next;
        switch (change.ChangeKind)
        {
            case CapacityChangeKind.Consume:
                Require(current.Status == ReservationStatus.Reserved && nowUtc.Kind == DateTimeKind.Utc &&
                    nowUtc < current.ValidUntilUtc, FinancialReasons.ReservationExpired, "Only a current unconsumed reservation can authorize submission.");
                Require(change.ExecutionId != Guid.Empty && unchanged, FinancialReasons.InvalidLifecycle, "Consumption requires execution identity and unchanged units.");
                next = ReservationStatus.Consumed;
                break;
            case CapacityChangeKind.ReleaseUnconsumed:
            case CapacityChangeKind.ExpireUnconsumed:
                Require(current.Status == ReservationStatus.Reserved && current.ExecutionId is null &&
                    change.FilledUnits == 0 && change.RemainingUnits == 0 && change.CancelledUnits == current.StrategyUnits,
                    FinancialReasons.InvalidLifecycle, "Only unconsumed capacity can be released without execution reconciliation.");
                Require(change.ChangeKind != CapacityChangeKind.ExpireUnconsumed || nowUtc >= current.ValidUntilUtc,
                    FinancialReasons.InvalidLifecycle, "Reservation has not expired.");
                next = change.ChangeKind == CapacityChangeKind.ExpireUnconsumed ? ReservationStatus.Expired : ReservationStatus.Released;
                break;
            case CapacityChangeKind.RecordPositionClose:
                Require(current.ExecutionId is not null && current.Status != ReservationStatus.Reserved && unchanged &&
                    change.ClosedUnits > current.ClosedUnits && !string.IsNullOrWhiteSpace(change.RelatedPostingReference),
                    FinancialReasons.InvalidLifecycle, "Closing exposure requires unchanged entry quantities and committed financial reconciliation.");
                next = change.RemainingUnits == 0 && change.ClosedUnits == change.FilledUnits
                    ? ReservationStatus.Released : current.Status;
                break;
            case CapacityChangeKind.RecordFill:
                Require(current.Status != ReservationStatus.Reserved && current.ExecutionId is not null &&
                    change.FilledUnits > current.FilledUnits && change.CancelledUnits == current.CancelledUnits,
                    FinancialReasons.InvalidLifecycle, "A fill requires consumed execution and a positive new fill quantity.");
                next = change.RemainingUnits == 0 ? ReservationStatus.Filled :
                    current.Status == ReservationStatus.CancelPending ? ReservationStatus.CancelPending : ReservationStatus.PartiallyFilled;
                break;
            case CapacityChangeKind.ConfirmCancel:
                Require(current.Status != ReservationStatus.Reserved && current.ExecutionId is not null &&
                    change.RemainingUnits == 0 && !string.IsNullOrWhiteSpace(change.RelatedPostingReference),
                    FinancialReasons.InvalidLifecycle, "Terminal cancellation requires reconciled execution/financial evidence.");
                next = change.FilledUnits == change.ClosedUnits ? ReservationStatus.Released : ReservationStatus.Filled;
                break;
            case CapacityChangeKind.RequestCancel:
                Require(current.Status != ReservationStatus.Reserved && unchanged, FinancialReasons.InvalidLifecycle, "Cancel request retains all current obligations.");
                next = ReservationStatus.CancelPending;
                break;
            case CapacityChangeKind.MarkSubmissionUnknown:
                Require(current.Status != ReservationStatus.Reserved && unchanged, FinancialReasons.InvalidLifecycle, "Unknown submission must retain all obligations.");
                next = ReservationStatus.SubmissionUnknown;
                break;
            case CapacityChangeKind.RecordWorking:
                Require(current.Status is ReservationStatus.Consumed or ReservationStatus.SubmissionUnknown or ReservationStatus.Working && unchanged,
                    FinancialReasons.InvalidLifecycle, "Working acknowledgement cannot change quantities or undo pending cancellation.");
                next = ReservationStatus.Working;
                break;
            default: throw new FinancialOperationException(FinancialReasons.InvalidLifecycle, "Unsupported lifecycle transition.");
        }
        return new(next, change.FilledUnits, change.CancelledUnits, change.RemainingUnits,
            next == ReservationStatus.Reserved ? (decimal)change.RemainingUnits / current.StrategyUnits : 0,
            next is not (ReservationStatus.Reserved or ReservationStatus.Released or ReservationStatus.Expired or ReservationStatus.Filled)
                ? (decimal)change.RemainingUnits / current.StrategyUnits : 0,
            (decimal)(change.FilledUnits - change.ClosedUnits) / current.StrategyUnits, change.ClosedUnits);
    }

    static void Require(bool condition, int code, string message)
    { if (!condition) throw new FinancialOperationException(code, message); }
}
