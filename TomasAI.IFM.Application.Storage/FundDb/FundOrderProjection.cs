namespace TomasAI.IFM.Application.Storage.FundDb;

internal readonly record struct FundOrderProjectionRow(
    int OrderId,
    int FundId,
    Guid? ReservationToken);

internal readonly record struct FundOrderReservation(
    int FundId,
    Guid? ReservationToken);

internal readonly record struct FundOrderWriteOwnership(
    int OrderId,
    Guid OperationId,
    DateTime StartedOn);

public readonly record struct FundOrderProjectionBackfillResult(
    long SourceRows,
    long ProjectedRows,
    long MissingRows,
    long ConflictingRows,
    long TokenlessRows = 0)
{
    public bool IsReconciled
        => MissingRows == 0 && ConflictingRows == 0 && TokenlessRows == 0;

    [Obsolete("Fund order IDs are permanent; use ConflictingRows.")]
    public long UnexpectedRows => ConflictingRows;
}
