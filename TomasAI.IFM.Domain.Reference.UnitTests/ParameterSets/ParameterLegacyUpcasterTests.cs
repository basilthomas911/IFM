using System.Text.Json;
using System.Text.Json.Nodes;
using TomasAI.IFM.Domain.Reference.ParameterSets.Migration;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

public sealed class ParameterLegacyUpcasterTests
{
    [Theory][MemberData(nameof(TypedParameterValueTests.Components), MemberType=typeof(TypedParameterValueTests))]
    public void Historical_conversion_preserves_reference_and_verifies_all_eight_parameter_families(string component)
    {
        var descriptor=ParameterComponentModelRegistry.Get(component);
        var id=Guid.NewGuid();
        var draft=JsonNode.Parse(descriptor.CreateDraftPayload(id))!.AsObject(); draft["Version"]=1;
        var json=draft.ToJsonString();
        var historical=new ParameterSetVersion(new(id,1,component,ParameterCanonicalPayloadModel.Hash(json)),
            "Historical", "",draft["SchemaVersion"]!.GetValue<int>(),ParameterVersionStatus.Draft,json,DateTime.UtcNow,"test");
        var converted=ParameterLegacyUpcaster.Verify(historical);
        Assert.Same(historical.Reference,converted.OriginalReference);
        Assert.Equal(component,converted.Parameters.ComponentCode);
        Assert.Equal(id,converted.Parameters.SetId);
        Assert.Equal(1,converted.Parameters.Version);
        Assert.Equal(historical.SchemaVersion,converted.Parameters.SchemaVersion);
        Assert.Equal(ParameterValueCodec.Encode(converted.Parameters),ParameterValueCodec.Encode(ParameterValueCodec.Decode(ParameterValueCodec.Encode(converted.Parameters))));
        Assert.Equal(ParameterValueCodec.Hash(converted.Parameters),converted.TypedContentSha256);
        Assert.Equal(json,historical.PayloadJson);
    }

    [Fact]
    public void Tampered_unknown_and_mismatched_historical_facts_are_rejected()
    {
        var descriptor=new FuturesItiSignalParameterModel();var id=Guid.NewGuid();
        var draft=JsonNode.Parse(descriptor.CreateDraftPayload(id))!.AsObject(); draft["Version"]=1;
        var json=draft.ToJsonString();
        var historical=new ParameterSetVersion(new(id,1,descriptor.Summary.ComponentCode,ParameterCanonicalPayloadModel.Hash(json)),
            "Historical", "",1,ParameterVersionStatus.Published,json,DateTime.UtcNow,"test");
        var tampered=JsonNode.Parse(json)!.AsObject();tampered["Version"]=2;
        Assert.Equal("PARAM.LEGACY_HASH_MISMATCH",Assert.Throws<InvalidDataException>(()=>ParameterLegacyUpcaster.Verify(historical with{PayloadJson=tampered.ToJsonString()})).Message);
        var unknown=JsonNode.Parse(json)!.AsObject();unknown["UnsupportedFutureProperty"]=17;
        var changed=unknown.ToJsonString();
        Assert.Equal("PARAM.LEGACY_FIELDS_UNSUPPORTED",Assert.Throws<InvalidDataException>(()=>ParameterLegacyUpcaster.Verify(historical with{PayloadJson=changed,Reference=historical.Reference with{PayloadSha256=ParameterCanonicalPayloadModel.Hash(changed)}})).Message);
        Assert.Equal("PARAM.LEGACY_IDENTITY_MISMATCH",Assert.Throws<InvalidDataException>(()=>ParameterLegacyUpcaster.Verify(historical with{Reference=historical.Reference with{SetId=Guid.NewGuid()}})).Message);
    }
}
