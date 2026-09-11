using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

public enum ParameterVersionStatus : byte { Draft = 0, Published = 1, Retired = 2 }
public enum ParameterApplicationPolicy : byte { Unknown = 0, NextOperation = 1, NextStartup = 2, ExplicitReload = 3 }
public enum ParameterIssueSeverity : byte { Unknown = 0, Information = 1, Warning = 2, Error = 3 }
public enum SignalRequirementMode : byte { Unknown = 0, Optional = 1, Required = 2 }
public enum SignalStartupPreparation : byte { None = 0, Initialize = 1, InitializeAndWarm = 2 }

[MessagePackObject]
public sealed record ParameterVersionRef(
    [property: Key(0)] Guid SetId, [property: Key(1)] int Version,
    [property: Key(2)] string ComponentCode, [property: Key(3)] string PayloadSha256);
[MessagePackObject]
public sealed record ParameterValidationIssue(
    [property: Key(0)] string Code, [property: Key(1)] string Path,
    [property: Key(2)] string Message,
    [property: Key(3)] ParameterIssueSeverity Severity = ParameterIssueSeverity.Error);
[MessagePackObject]
public sealed record ParameterComponentSummary(
    [property: Key(0)] string AreaCode, [property: Key(1)] string AreaName,
    [property: Key(2)] string ComponentCode, [property: Key(3)] string Name,
    [property: Key(4)] int[] SchemaVersions, [property: Key(5)] bool CanEdit);
[MessagePackObject]
public sealed record ParameterSetVersion(
    [property: Key(0)] ParameterVersionRef Reference,
    [property: Key(1)] string Name, [property: Key(2)] string Description,
    [property: Key(3)] int SchemaVersion, [property: Key(4)] ParameterVersionStatus Status,
    [property: Key(5)] string PayloadJson, [property: Key(6)] DateTime CreatedAtUtc,
    [property: Key(7)] string CreatedBy, [property: Key(8)] DateTime? PublishedAtUtc = null,
    [property: Key(9)] DateTime? RetiredAtUtc = null, [property: Key(10)] long CatalogRevision = 0,
    [property: Key(11)] ParameterLegacyReference? LegacySource = null);
[MessagePackObject]
public sealed record ParameterValidationReport(
    [property: Key(0)] string CandidateSha256,
    [property: Key(1)] ParameterValidationIssue[] Issues)
{
    [IgnoreMember] public bool IsValid => Issues.All(x => x.Severity != ParameterIssueSeverity.Error);
}

public interface IParameterComponentDescriptor
{
    ParameterComponentSummary Summary { get; }
    string CreateDraftPayload(Guid setId);
    ParameterValidationIssue[] Validate(string payloadJson, int schemaVersion);
}

[MessagePackObject]
public sealed record ParameterOperationReceipt(
    [property: Key(0)] Guid OperationId,
    [property: Key(1)] string RequestSha256,
    [property: Key(2)] long Revision,
    [property: Key(3)] ParameterVersionRef Reference);

/// <summary>Authoritative event-stream state, independent of list projection visibility.</summary>
[MessagePackObject]
public sealed record ParameterSetSnapshot(
    [property: Key(0)] Guid SetId,
    [property: Key(1)] long Revision,
    [property: Key(2)] ParameterSetVersion[] Versions,
    [property: Key(3)] ParameterOperationReceipt[] Operations,
    [property: Key(4)] ParameterAuditEntry[]? Audit = null);
