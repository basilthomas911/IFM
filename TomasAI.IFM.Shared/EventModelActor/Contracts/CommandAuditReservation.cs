namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>
/// Describes the result of durably auditing and reserving a command identifier.
/// </summary>
/// <param name="Accepted">
/// <see langword="true"/> when this caller created the durable command audit record;
/// <see langword="false"/> when the command identifier was already reserved.
/// </param>
public readonly record struct CommandAuditReservation(bool Accepted);
