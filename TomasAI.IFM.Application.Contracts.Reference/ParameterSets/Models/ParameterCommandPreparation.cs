using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>A closed preparation request containing exactly one cross-owner mutation.</summary>
[MessagePackObject]
public sealed record ParameterCommandPreparation(
    [property: Key(0)] AssignParameterVersionCommand? Assignment = null,
    [property: Key(1)] RetireParameterVersionCommand? Retirement = null,
    [property: Key(2)] PublishParameterVersionCommand? Publication = null,
    [property: Key(3)] ApplySignalStartupPlanCommand? Startup = null,
    [property: Key(4)] CreateParameterSetCommand? Creation = null);
