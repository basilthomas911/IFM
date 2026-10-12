namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
public enum DatabaseRecoveryPhase
{
    None = 0, Requested = 1, Authorized = 2, Admitted = 3, Preflight = 4, Started = 5,
    EstablishingBoundary = 6, Capturing = 7, Transferring = 8, Verifying = 9,
    Validating = 10, ReadyForCutover = 11, CuttingOver = 12, Reconciling = 13,
    Completed = 14, Failed = 15, Cancelled = 16, Rejected = 17
}
