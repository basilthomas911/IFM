using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

public sealed record ParameterRuntimeResolution(bool IsConfigured, bool IsDisabled, AppliedParameterAssignment? Applied);
