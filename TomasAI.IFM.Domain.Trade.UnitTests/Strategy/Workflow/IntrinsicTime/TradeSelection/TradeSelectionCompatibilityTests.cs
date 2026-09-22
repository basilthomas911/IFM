using FluentAssertions;
using FluentAssertions.Execution;
using MessagePack;
using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
[Trait("Gate","TS-01")]
public sealed class TradeSelectionCompatibilityTests
{
    [Fact]
    public void Extracted_contracts_keep_the_captured_post_legacy_cutover_wire_and_json_hash_vectors()
    {
        using var scope = new AssertionScope();
        var key=new CatalogKey(StrategyCatalogKind.Deployment,Guid.Parse("11111111-2222-3333-4444-555555555555"),7);
        Verify(new PortfolioFundStrategySnapshot {WorkflowId=key.Id,WorkflowRevision=8,Fund=new(){FundId=42,PermittedTradeStrategyFamilies=[new(0,0){CatalogDeployment=key}]}},
            "3A2804AD10A65AF26DB5D42FEF137DA1CF820741097D70A00B5EE9EE04164395","FA1F837C3CF47C0B5F0C25EBBAD0AFFC57D8B72D72872D63F31DC81070E0463B");
        Verify(new TradeStrategyFamilyReference(0,0){CatalogDeployment=key},"BC211AD405D6DC312A0A24303C7BA59EEC30EE432C7E0826ADC5CAECDD618B86","050FFACA2C10130B8E5BDE51945D71372FE5C3200D8A88758C81DD0E2F8AF36E");
        Verify(new ReserveFundOrderCompositionRequest {WorkflowId=key.Id,WorkflowRevision=8,TradeTemplateId=key.Id,TradeTemplateVersion=7,TradeInstructions=[new(){UnderlyingRoot="ES",TradeFamily="Condor",DirectionOrBias="Neutral"}]},
            "BEBE2DE6617E3D6936AFE77B1F252C2C710B41890D682B84291EC711F9526170","638A3ABB843E720AFECAF7E3A99CA84289C20A7DA4FD2B22EB91601BF6C42504");
    }
    static void Verify<T>(T value,string wire,string json)
    {
        Convert.ToHexString(SHA256.HashData(MessagePackSerializer.Serialize(value))).Should().Be(wire);
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).Should().Be(json);
    }
}
