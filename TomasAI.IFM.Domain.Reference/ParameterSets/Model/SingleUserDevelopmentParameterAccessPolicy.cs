using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Explicit development-only policy for the trusted single-user application host.</summary>
public sealed class SingleUserDevelopmentParameterAccessPolicy(string environmentName, bool enabled) : IParameterAccessPolicy
{
    public void Demand(ParameterCapability capability)
    {
        if (!Enum.IsDefined(capability)) throw new UnauthorizedAccessException("PARAM.CAPABILITY_UNKNOWN");
        if (!enabled || !string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("PARAM.DEVELOPMENT_ACCESS_DISABLED");
    }
}
