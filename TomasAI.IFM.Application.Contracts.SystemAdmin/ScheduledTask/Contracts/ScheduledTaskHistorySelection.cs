namespace TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;

/// <summary>Accepted filters and decoded keyset for a persisted run-history page.</summary>
/// <param name="Environment">The validated environment scope.</param>
/// <param name="Host">The validated owning host.</param>
/// <param name="Schedule">The schedule identity.</param>
/// <param name="PageSize">The bounded number of returned runs.</param>
/// <param name="Scope">The domain-prepared continuation scope hash.</param>
/// <param name="After">The validated continuation key, when reading a later page.</param>
public sealed record ScheduledTaskHistorySelection(string Environment, string Host, ScheduledTaskId Schedule,
    int PageSize, string Scope, ScheduledTaskHistoryCursor? After);

/// <summary>Opaque continuation layout retained for existing clients.</summary>
/// <param name="Scope">The immutable filter-scope hash.</param>
/// <param name="Fire">The UTC occurrence boundary.</param>
/// <param name="Run">The tie-breaking run identity.</param>
public sealed record ScheduledTaskHistoryCursor(string Scope, DateTime Fire, Guid Run);
