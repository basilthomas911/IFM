using MessagePack;

namespace TomasAI.IFM.Domain.Application.Shared;

/// <summary>One immutable step in the authoritative sequential startup plan.</summary>
public sealed record ApplicationStartupActivityDefinition(
    ApplicationStartupActivity Activity,
    bool Required,
    IReadOnlyCollection<ApplicationStartupActivity> Dependencies);
