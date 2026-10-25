using MessagePack;

namespace TomasAI.IFM.Domain.Application.Shared;

/// <summary>Stable identity and correlation supplied to every startup activity.</summary>
public sealed record ApplicationStartupContext(
    DateOnly ValueDate,
    Guid ProcessBootId,
    Guid CommandId,
    Guid CorrelationId);
