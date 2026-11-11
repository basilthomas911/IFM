using TomasAI.IFM.Shared.MarketData.Definitions;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Contracts;
using Xunit;
namespace TomasAI.IFM.Application.MarketData.UnitTests;
public class InstrumentDefinitionSelectionMultiplierTests
{
 [Fact] public void Es_price_point_multiplier_uses_exact_quantity_and_tick_economics_not_deliverables(){var row=Definition();Assert.Null(row.Summary.ContractMultiplier);Assert.Equal(50m,InstrumentDefinitionSelectionMapper.Map(Guid.NewGuid(),0,row).Multiplier);}
 [Fact] public void Mes_price_point_multiplier_is_verified_from_provider_quantity_and_tick_value(){var row=Definition("MES","12500000","5000000000");Assert.Null(row.Summary.ContractMultiplier);Assert.Equal(5m,InstrumentDefinitionSelectionMapper.Map(Guid.NewGuid(),0,row).Multiplier);}
 [Fact] public void Mes_conflicting_tick_economics_remain_unknown(){Assert.Null(InstrumentDefinitionSelectionMapper.PricePointMultiplier(Definition("MES","12600000","5000000000")));}
 [Fact] public void Variable_tick_es_option_preserves_quantity_without_inventing_a_fixed_tick(){var future=Definition();var option=TomasAI.IFM.Shared.MarketData.Definitions.ExactInstrumentDefinition.Parse("GLBX.MDP3",future.Json.Replace("\"instrument_class\":\"F\"","\"instrument_class\":\"P\"").Replace("250000000","9223372036854775807").Replace("125000000","0"));Assert.Equal(50m,InstrumentDefinitionSelectionMapper.PricePointMultiplier(option));Assert.Null(option.Summary.MinimumPriceIncrement);}
 [Theory][InlineData("CL","125000000")][InlineData("ES","126000000")][InlineData("ES","9223372036854775807")]
 public void Unsupported_or_conflicting_economics_remain_unknown(string asset,string tickAmount){Assert.Null(InstrumentDefinitionSelectionMapper.PricePointMultiplier(Definition(asset,tickAmount)));}
 static TomasAI.IFM.Shared.MarketData.Definitions.ExactInstrumentDefinition Definition(string asset="ES",string amount="125000000",string quantity="50000000000"){
  var source=InstrumentDefinitionRefreshTests.Definition(42,"ESZ6",asset,"F");using var doc=JsonDocument.Parse(source.Json);
  var fields=doc.RootElement.EnumerateObject().ToDictionary(x=>x.Name,x=>(object)x.Value.Clone());
  fields["unit_of_measure"]="IPNT";fields["unit_of_measure_qty"]=quantity;fields["display_factor"]="10000000";fields["min_price_increment"]="250000000";fields["min_price_increment_amount"]=amount;fields["contract_multiplier"]=int.MaxValue;
  return TomasAI.IFM.Shared.MarketData.Definitions.ExactInstrumentDefinition.Parse("GLBX.MDP3",JsonSerializer.Serialize(fields));
 }
}
