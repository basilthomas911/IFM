namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Capabilities enforced by Parameter Sets handlers.</summary>
public enum ParameterCapability : byte { Read = 1, Author = 2, Publish = 3, Assign = 4, Retire = 5 }

/// <summary>Host-owned access boundary; payload identities do not grant access.</summary>
public interface IParameterAccessPolicy
{
    void Demand(ParameterCapability capability);
}
