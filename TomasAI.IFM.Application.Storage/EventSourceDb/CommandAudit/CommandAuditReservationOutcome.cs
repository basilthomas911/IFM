namespace TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

/// <summary>Reports whether a prepared command audit was newly reserved or already present.</summary>
public enum CommandAuditReservationOutcome
{
    Accepted,
    Existing,
    PayloadConflict
}
