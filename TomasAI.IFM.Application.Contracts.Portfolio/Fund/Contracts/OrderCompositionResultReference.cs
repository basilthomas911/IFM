using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

[MessagePackObject(AllowPrivate = true)]
public sealed record OrderCompositionResultReference
{
    [Key(0)] public Guid ResultId { get; init; }
    [Key(1)] public string ResultSha256 { get; init; } = string.Empty;
    [Key(2)] public DateTime EvaluatedAtUtc { get; init; }
    [Key(3)] public DateTime ExpiresAtUtc { get; init; }
    [Key(4)] public Guid InvocationId { get; init; }
}
