using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Model;

/// <summary>Defines the two application-wide Market Selection option-spread assignment scopes.</summary>
public static class OptionSpreadStrategyParameterScopeModel
{
    public const string ConsumerKind = "market-selection";
    public const string ConsumerId = "option-spread-strategy";

    public static ParameterAssignmentScope IronCondor() => Create(
        IronCondorMarketSelectionParameterModel.ComponentCode, "iron-condor-defaults", "IronCondor");
    public static ParameterAssignmentScope VerticalSpread() => Create(
        VerticalSpreadMarketSelectionParameterModel.ComponentCode, "vertical-spread-defaults", "VerticalSpread");

    public static void Validate(ParameterAssignmentScope scope)
    {
        var expected = scope.ComponentCode switch
        {
            IronCondorMarketSelectionParameterModel.ComponentCode => IronCondor(),
            VerticalSpreadMarketSelectionParameterModel.ComponentCode => VerticalSpread(),
            _ => throw new ArgumentException("PARAM.COMPONENT_UNSUPPORTED")
        };
        if (scope != expected) throw new ArgumentException("PARAM.SCOPE_INVALID");
    }

    public static Guid AssignmentId(ParameterAssignmentScope scope)
    {
        Validate(scope);
        var identity = JsonSerializer.Serialize(new
            { scope.ConsumerKindCode, scope.ConsumerId, scope.Role, scope.ComponentCode, scope.ScopeSha256 });
        return new(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0, 16));
    }

    static ParameterAssignmentScope Create(string component, string role, string strategy)
    {
        var json = ParameterCanonicalPayloadModel.Canonicalize(JsonSerializer.Serialize(new { Strategy = strategy }));
        return new(ConsumerKind, ConsumerId, role, component, json, ParameterCanonicalPayloadModel.Hash(json));
    }
}
