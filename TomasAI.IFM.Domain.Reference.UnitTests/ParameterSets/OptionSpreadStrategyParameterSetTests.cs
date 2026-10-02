using System.Text.Json;
using FluentAssertions;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

public sealed class OptionSpreadStrategyParameterSetTests
{
    [Fact]
    public void Components_expose_independent_strategy_views_with_ES_defaults()
    {
        var iron = new IronCondorMarketSelectionParameterModel();
        var vertical = new VerticalSpreadMarketSelectionParameterModel();
        iron.Summary.AreaName.Should().Be("Option Spread Strategy Defaults");
        iron.Summary.Name.Should().Be("Iron Condor");
        vertical.Summary.AreaName.Should().Be("Option Spread Strategy Defaults");
        vertical.Summary.Name.Should().Be("Vertical Spreads");
        var ironValue = JsonSerializer.Deserialize<IronCondorMarketSelectionParameterSet>(iron.CreateDraftPayload(Guid.NewGuid()))!;
        ironValue.DefaultSymbol.Should().Be("ES");
        ironValue.Symbols.Should().ContainSingle().Which.Should().BeEquivalentTo(new IronCondorSymbolDefaults
            { Symbol = "ES", ShortCallDelta = 16, CallSpreadWidth = 50, ShortPutDelta = 16, PutSpreadWidth = 50 });
        var verticalValue = JsonSerializer.Deserialize<VerticalSpreadMarketSelectionParameterSet>(vertical.CreateDraftPayload(Guid.NewGuid()))!;
        verticalValue.Symbols.Should().ContainSingle().Which.Should().BeEquivalentTo(new VerticalSpreadSymbolDefaults
            { Symbol = "ES", ShortLegDelta = 16, SpreadWidth = 50 });
    }

    [Fact]
    public void Validation_rejects_duplicate_symbols_and_out_of_range_delta()
    {
        var model = new IronCondorMarketSelectionParameterModel();
        var setId = Guid.NewGuid();
        var payload = IronCondorMarketSelectionParameterModel.CreateDefault(setId) with
        {
            Symbols =
            [
                new() { Symbol = "ES", ShortCallDelta = 0, CallSpreadWidth = 50, ShortPutDelta = 16, PutSpreadWidth = 50 },
                new() { Symbol = "ES", ShortCallDelta = 16, CallSpreadWidth = 50, ShortPutDelta = 16, PutSpreadWidth = 50 }
            ]
        };
        var issues = model.Validate(JsonSerializer.Serialize(payload), 1);
        issues.Should().Contain(issue => issue.Path == "Symbols");
        issues.Should().Contain(issue => issue.Path == "Symbols/0/ShortCallDelta");
    }

    [Fact]
    public void Iron_condor_assignment_uses_component_policy_and_enters_startup_snapshot()
    {
        var setId = Guid.NewGuid();
        var payload = IronCondorMarketSelectionParameterModel.CreateDefault(setId) with { Version = 1 };
        var json = ParameterCanonicalPayloadModel.Canonicalize(JsonSerializer.Serialize(payload));
        var version = new ParameterSetVersion(new(setId, 1, IronCondorMarketSelectionParameterModel.ComponentCode,
            ParameterCanonicalPayloadModel.Hash(json)), "Iron Condor", "", 1, ParameterVersionStatus.Published,
            json, DateTime.UtcNow, "test", DateTime.UtcNow);
        var scope = OptionSpreadStrategyParameterScopeModel.IronCondor();
        var assignment = ParameterAssignmentModel.Assign(scope, version, null, 0, DateTime.UtcNow, "test");
        assignment.AssignmentId.Should().Be(ParameterAssignmentPolicyModel.AssignmentId(scope));
        var startup = new ParameterStartupSnapshotModel(Guid.NewGuid(), [assignment],
            new Dictionary<ParameterVersionRef, ParameterSetVersion> { [version.Reference] = version });
        startup.Resolve(scope, startup.StartupRunId)!.Version.Should().Be(version);
        var run = new ParameterStartupRun(startup.StartupRunId, [assignment], [version],
            new(startup.StartupRunId, startup.Fingerprint, startup.Fingerprint, []),
            DateTime.UtcNow, "test");
        var runtime = new ParameterRuntimeSnapshotModel(true);
        runtime.Apply(run);
        runtime.Resolve(scope).Applied!.Version.Should().Be(version);
    }
}
