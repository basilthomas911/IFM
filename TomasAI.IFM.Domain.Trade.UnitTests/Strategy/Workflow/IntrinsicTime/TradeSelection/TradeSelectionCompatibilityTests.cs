using FluentAssertions;
using FluentAssertions.Execution;
using MessagePack;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Buffers;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;

[Trait("Gate", "TS-01")]
public sealed class TradeSelectionCompatibilityTests
{
    [Fact]
    public void Extracted_contracts_keep_the_captured_post_legacy_cutover_wire_and_json_hash_vectors()
    {
        using var scope = new AssertionScope();
        var key = new CatalogKey(StrategyCatalogKind.Deployment, Guid.Parse("11111111-2222-3333-4444-555555555555"), 7);
        var fundSnapshot = new PortfolioFundStrategySnapshot { WorkflowId = key.Id, WorkflowRevision = 8, Fund = new() { FundId = 42, PermittedTradeStrategyFamilies = [new(0, 0) { CatalogDeployment = key }] } };
        Verify(fundSnapshot,
            "60D894E4B47D204353F897B6930C1D751DC01CB7A7103C6B3BD4EA554DCE6F0E", "225586804A82A931AAD10B03407791AA3FF7761C26353752E10B17F6CE6ED973");
        VerifyLegacyFundSnapshot(fundSnapshot, key);
        Verify(new TradeStrategyFamilyReference(0, 0) { CatalogDeployment = key }, "BC211AD405D6DC312A0A24303C7BA59EEC30EE432C7E0826ADC5CAECDD618B86", "050FFACA2C10130B8E5BDE51945D71372FE5C3200D8A88758C81DD0E2F8AF36E");
        Verify(new ReserveFundOrderCompositionRequest { WorkflowId = key.Id, WorkflowRevision = 8, TradeTemplateId = key.Id, TradeTemplateVersion = 7, TradeInstructions = [new() { UnderlyingRoot = "ES", TradeFamily = "Condor", DirectionOrBias = "Neutral" }] },
            "BEBE2DE6617E3D6936AFE77B1F252C2C710B41890D682B84291EC711F9526170", "638A3ABB843E720AFECAF7E3A99CA84289C20A7DA4FD2B22EB91601BF6C42504");
    }

    // BaseContracts was appended at Fund key 22. Check every earlier byte against
    // the captured baseline and prove both legacy formats still deserialize.
    static void VerifyLegacyFundSnapshot(PortfolioFundStrategySnapshot snapshot, CatalogKey deployment)
    {
        var reader = new MessagePackReader(new ReadOnlySequence<byte>(MessagePackSerializer.Serialize(snapshot)));
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        var fields = reader.ReadArrayHeader();
        writer.WriteArrayHeader(fields);
        for (var index = 0; index < fields; index++)
        {
            if (index != 4) { writer.WriteRaw(reader.ReadRaw()); continue; }
            var fundFields = reader.ReadArrayHeader();
            fundFields.Should().Be(23);
            writer.WriteArrayHeader(fundFields - 1);
            for (var field = 0; field < fundFields; field++)
            {
                var raw = reader.ReadRaw();
                if (field < 22) writer.WriteRaw(raw);
            }
        }
        writer.Flush();
        Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan)).Should().Be("3A2804AD10A65AF26DB5D42FEF137DA1CF820741097D70A00B5EE9EE04164395");
        var legacy = MessagePackSerializer.Deserialize<PortfolioFundStrategySnapshot>(buffer.WrittenMemory);
        legacy.Fund.BaseContracts.Should().BeEmpty();
        legacy.Fund.PermittedTradeStrategyFamilies.Single().CatalogDeployment.Should().Be(deployment);
        var json = JsonNode.Parse(JsonSerializer.Serialize(snapshot))!;
        json["Fund"]!.AsObject().Remove("BaseContracts").Should().BeTrue();
        var legacyJson = JsonSerializer.SerializeToUtf8Bytes(json);
        Convert.ToHexString(SHA256.HashData(legacyJson)).Should().Be("FA1F837C3CF47C0B5F0C25EBBAD0AFFC57D8B72D72872D63F31DC81070E0463B");
        JsonSerializer.Deserialize<PortfolioFundStrategySnapshot>(legacyJson)!.Fund.BaseContracts.Should().BeEmpty();
    }
    static void Verify<T>(T value, string wire, string json)
    {
        Convert.ToHexString(SHA256.HashData(MessagePackSerializer.Serialize(value))).Should().Be(wire);
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value))).Should().Be(json);
    }
}
