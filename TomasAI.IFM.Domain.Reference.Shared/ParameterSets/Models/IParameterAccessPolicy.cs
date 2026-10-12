namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Host-owned access boundary; payload identities do not grant access.</summary>
public interface IParameterAccessPolicy
{
    void Demand(ParameterCapability capability);
}
