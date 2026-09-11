using FluentAssertions;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;
public sealed class ParameterAccessPolicyTests
{
    [Theory]
    [InlineData("Production",true)]
    [InlineData("Staging",true)]
    [InlineData("Development",false)]
    public void Development_policy_cannot_enable_other_environments(string environment,bool enabled)
    {
        var policy=new SingleUserDevelopmentParameterAccessPolicy(environment,enabled);
        foreach(var capability in Enum.GetValues<ParameterCapability>())
        {
            Action action=()=>policy.Demand(capability);
            action.Should().Throw<UnauthorizedAccessException>();
        }
    }
    [Fact] public void Explicit_development_policy_grants_only_known_capabilities()
    {
        var policy=new SingleUserDevelopmentParameterAccessPolicy("Development",true);
        foreach(var capability in Enum.GetValues<ParameterCapability>())policy.Demand(capability);
        Action unknown=()=>policy.Demand((ParameterCapability)255);
        unknown.Should().Throw<UnauthorizedAccessException>();
    }
}
