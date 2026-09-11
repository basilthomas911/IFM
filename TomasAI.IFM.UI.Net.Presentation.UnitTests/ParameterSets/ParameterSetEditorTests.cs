using System.Text.Json;
using FluentAssertions;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.UI.Net.ViewModels.Reference.ParameterSets;
namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ParameterSets;

public sealed class ParameterSetEditorTests
{
    static ParameterSetVersion Version()
    {
        var id=Guid.NewGuid();
        var payload=RegimeDiscoveryParameterSet.CreateDefault(id,Guid.NewGuid(),TimeFrameType.Daily)
            with {SchemaVersion=2,SignalRequirements=[new(){RequirementId=Guid.NewGuid(),Enabled=true}]};
        return new(new(id,1,"strategy-workflow.regime-discovery",new string('a',64)),"Daily","",2,
            ParameterVersionStatus.Published,JsonSerializer.Serialize(payload),DateTime.UtcNow,"test",CatalogRevision:3);
    }
    [Fact] public void Unknown_payload_fields_are_preserved_exactly_and_cannot_be_edited()
    {
        var saved=Version();var json=System.Text.Json.Nodes.JsonNode.Parse(saved.PayloadJson)!;json["FutureField"]=123;
        saved=saved with {PayloadJson=json.ToJsonString()};var editor=new ParameterSetEditorModel();editor.Load(saved,3);
        editor.CanEdit.Should().BeFalse();editor.Payload().Should().Be(saved.PayloadJson);editor.SetId.Should().Be(saved.Reference.SetId);
        Action edit=()=>editor.BeginEdit();edit.Should().Throw<InvalidOperationException>();
    }
    [Fact] public void Unknown_schema_remains_inspectable()
    {
        var saved=Version() with {SchemaVersion=99,PayloadJson="{\"SchemaVersion\":99,\"Future\":true}"};
        var editor=new ParameterSetEditorModel();editor.Load(saved,3);
        editor.Parameters.Should().BeNull();editor.CanEdit.Should().BeFalse();editor.Payload().Should().Be(saved.PayloadJson);
    }
    [Fact] public void Editing_a_working_copy_does_not_mutate_a_published_version()
    {
        var saved=Version();var editor=new ParameterSetEditorModel();editor.Load(saved,3);
        Action change=()=>editor.SetSignals([]);change.Should().Throw<InvalidOperationException>();
        editor.BeginEdit();editor.SetSignals(editor.Parameters!.SignalRequirements!.Select(x=>x with {Enabled=false}).ToArray());
        editor.Parameters.SignalRequirements![0].Enabled.Should().BeFalse();
        JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(saved.PayloadJson)!.SignalRequirements![0].Enabled.Should().BeTrue();
        editor.ExpectedRevision.Should().Be(3);
    }
    [Fact] public void Commit_acknowledgement_requires_a_fresh_read_before_another_edit()
    {
        var editor=new ParameterSetEditorModel();var saved=Version();editor.Load(saved,3);editor.BeginEdit();editor.AcknowledgeSave();
        editor.IsEditing.Should().BeFalse();editor.AwaitingRefresh.Should().BeTrue();
        Action edit=()=>editor.BeginEdit();edit.Should().Throw<InvalidOperationException>();
        editor.Load(saved,4);editor.BeginEdit();editor.IsEditing.Should().BeTrue();editor.ExpectedRevision.Should().Be(4);
    }
    [Fact] public void Generic_field_edits_preserve_hidden_identity_and_reject_type_changes()
    {
        const string source="{\"ParameterSetId\":\"unchanged\",\"Enabled\":true,\"Settings\":{\"Weight\":0.5}}";
        var fields=ParameterFieldEditorModel.Read(source);
        fields.Should().NotContain(x=>x.Path=="/ParameterSetId");
        fields.Single(x=>x.Path=="/Enabled").Should().Match<ParameterField>(x=>x.Group=="General"&&x.Name=="Enabled");
        var result=ParameterFieldEditorModel.Apply(source,fields.Select(x=>x.Path=="/Settings/Weight"?x with {Value="0.75"}:x));
        using var parsed=JsonDocument.Parse(result);
        parsed.RootElement.GetProperty("ParameterSetId").GetString().Should().Be("unchanged");
        parsed.RootElement.GetProperty("Settings").GetProperty("Weight").GetDecimal().Should().Be(0.75m);
        Action invalid=()=>ParameterFieldEditorModel.Apply(source,[fields.Single(x=>x.Path=="/Settings/Weight") with {Value="\"wrong\""}]);
        invalid.Should().Throw<ArgumentException>();
    }
    [Fact] public void Calculation_fields_are_grouped_by_root_and_have_friendly_relative_names()
    {
        const string source="{\"Trend\":{\"EmaAlignmentWeight\":0.25},\"DataQuality\":{\"SupportedSignalSchemaVersions\":[1,2]}}";
        var fields=ParameterFieldEditorModel.Read(source);
        ParameterFieldEditorModel.Groups(fields).Should().Equal("Trend","Data Quality");
        fields.Single(x=>x.Path=="/Trend/EmaAlignmentWeight").Should().Match<ParameterField>(x=>x.Group=="Trend"&&x.Name=="Ema Alignment Weight");
        fields.Single(x=>x.Path=="/DataQuality/SupportedSignalSchemaVersions/1").Name.Should().Be("Supported Signal Schema Versions [1]");
    }
    [Fact] public void Comparison_ignores_property_order_and_distinguishes_absent_from_null()
    {
        ParameterComparisonModel.Compare("{\"a\":1,\"b\":2}","{\"b\":2,\"a\":1}").Should().BeEmpty();
        var changes=ParameterComparisonModel.Compare("{\"rows\":[true]}","{\"rows\":[false],\"new\":null}");
        changes.Should().Contain(x=>x.Path=="/rows/0"&&x.Before=="true"&&x.After=="false");
        changes.Should().Contain(x=>x.Path=="/new"&&x.Before=="(absent)"&&x.After=="null");
    }
    [Fact] public void Editing_legacy_version_upgrades_only_the_working_copy_to_membership_semantics()
    {
        var saved=Version();var editor=new ParameterSetEditorModel();editor.Load(saved,3);
        var source=editor.Parameters!;
        var preview=source with {SchemaVersion=ParameterSchemaRegistry.CurrentRegimeSchemaVersion,Horizon=source.Horizon with {TimeFrames=source.Horizon.TimeFrames.Select(x=>x with {IsRequired=true}).ToArray()},SignalRequirements=source.SignalRequirements!.Select(x=>x with {IsRequired=true}).ToArray()};
        editor.BeginEdit(JsonSerializer.Serialize(preview));
        editor.Parameters!.SchemaVersion.Should().Be(ParameterSchemaRegistry.CurrentRegimeSchemaVersion);
        editor.Parameters.Horizon.TimeFrames.Should().OnlyContain(x=>x.IsRequired);
        editor.Parameters.SignalRequirements.Should().OnlyContain(x=>x.IsRequired);
        JsonSerializer.Deserialize<RegimeDiscoveryParameterSet>(saved.PayloadJson)!.SchemaVersion.Should().Be(2);
        ParameterFieldEditorModel.Read(editor.Payload()).Should().NotContain(x=>x.Path.EndsWith("/IsRequired"));
    }
}
