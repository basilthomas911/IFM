using FluentAssertions;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;

public sealed class RiskCatalogCapabilityTests
{
    [Fact]
    public void Implemented_risk_capabilities_cover_exact_structures_and_reject_mismatched_builder_claims()
    {
        var validators=RiskCatalogCapabilities.Create();
        validators.Select(x=>x.Capability.Code).Should().BeEquivalentTo("Future","CallVertical","PutVertical","IronCondor");
        var examples=StrategyCatalogExamples.Create();
        foreach(var validator in validators)
        {
            var structure=examples.Single(x=>x.Key.Kind==StrategyCatalogKind.Structure && x.Capabilities.Contains(new CatalogCapability("builder",validator.Capability.Code,1)));
            validator.Validate(structure,new Dictionary<CatalogKey,StoredStrategyCatalogDefinition>());
            Action missingBuilder=()=>validator.Validate(structure with { Capabilities=[validator.Capability] },new Dictionary<CatalogKey,StoredStrategyCatalogDefinition>());
            missingBuilder.Should().Throw<ArgumentException>().WithMessage("RM.CONFIG.UNSUPPORTED_STRUCTURE");
            Action empty=()=>validator.Validate(structure with { Legs=[] },new Dictionary<CatalogKey,StoredStrategyCatalogDefinition>());
            empty.Should().Throw<ArgumentException>();
        }
    }
}
