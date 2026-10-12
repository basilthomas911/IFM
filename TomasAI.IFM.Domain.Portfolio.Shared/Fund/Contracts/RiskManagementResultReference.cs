using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

[MessagePackObject(AllowPrivate = true)]
public sealed record RiskManagementResultReference
{
    [Key(0)] public Guid ResultId { get; init; }
    [Key(1)] public string ResultSha256 { get; init; } = string.Empty;
    [Key(2)] public RiskDecision Decision { get; init; }
    [Key(3)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(4)] public DateTime ExpiresAtUtc { get; init; }
    [Key(5)] public Guid EnvelopeId { get; init; }
    [Key(6)] public long EnvelopeVersion { get; init; }
    [Key(7)] public string CandidateSha256 { get; init; } = string.Empty;
}
