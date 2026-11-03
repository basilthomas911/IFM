namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
/// <summary>Common operation identity, revision and already accepted service observations.</summary>
internal sealed record DatabaseOperationDecisionState(DatabaseBackupOperationState Operation, IReadOnlyDictionary<Guid, string> ProcessedServiceEvents);
