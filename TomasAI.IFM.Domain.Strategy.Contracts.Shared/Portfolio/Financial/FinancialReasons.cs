namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Append-only allocation within the Portfolio 34000–34299 registry.</summary>
public static class FinancialReasons
{
    public const int InvalidContract = 34100;
    public const int AuthorityDenied = 34101;
    public const int UnbalancedJournal = 34102;
    public const int InvalidAccount = 34103;
    public const int UnsupportedCurrency = 34104;
    public const int ClosedPeriod = 34105;
    public const int InsufficientCash = 34106;
    public const int SourceConflict = 34107;
    public const int AlreadyPosted = 34108;
    public const int ExcessReversal = 34109;
    public const int UnreconciledMigration = 34110;
    public const int InsufficientCapacity = 34111;
    public const int AuthorityRevoked = 34112;
    public const int RevisionConflict = 34113;
    public const int RequestMismatch = 34114;
    public const int ReservationExpired = 34115;
    public const int InvalidLifecycle = 34116;
    public const int CommitUnknown = 34117;
    public const int PersistenceFailed = 34118;
    public const int TimeExpired = 34119;

    public static string Name(int code) => code switch
    {
        InvalidContract => "GL.CONTRACT.INVALID", AuthorityDenied => "GL.AUTHORITY.DENIED",
        UnbalancedJournal => "GL.JOURNAL.UNBALANCED", InvalidAccount => "GL.ACCOUNT.INVALID",
        UnsupportedCurrency => "GL.CURRENCY.UNSUPPORTED", ClosedPeriod => "GL.PERIOD.CLOSED",
        InsufficientCash => "GL.CASH.INSUFFICIENT", SourceConflict => "GL.SOURCE.CONFLICT",
        AlreadyPosted => "GL.SOURCE.ALREADY_POSTED", ExcessReversal => "GL.REVERSAL.EXCESS",
        UnreconciledMigration => "GL.MIGRATION.UNRECONCILED", InsufficientCapacity => "CR.CAPACITY.INSUFFICIENT",
        AuthorityRevoked => "CR.AUTHORITY.REVOKED", RevisionConflict => "CR.REVISION.CONFLICT",
        RequestMismatch => "CR.REQUEST.MISMATCH", ReservationExpired => "CR.RESERVATION.EXPIRED",
        InvalidLifecycle => "CR.LIFECYCLE.INVALID", CommitUnknown => "FIN.COMMIT.UNKNOWN",
        PersistenceFailed => "FIN.PERSISTENCE.FAILED", TimeExpired => "FIN.TIME.EXPIRED",
        _ => throw new ArgumentOutOfRangeException(nameof(code))
    };
}

/// <summary>A classified refusal carries whether this attempt changed financial authority.</summary>
public sealed class FinancialOperationException(int code, string message,
    FinancialCommitDisposition disposition = FinancialCommitDisposition.NotCommitted, Guid? existingOperationId = null)
    : Exception(message)
{
    public int Code { get; } = code;
    public string Reason => FinancialReasons.Name(Code);
    public FinancialCommitDisposition Disposition { get; } = disposition;
    public Guid? ExistingOperationId { get; } = existingOperationId;
}
