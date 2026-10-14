using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Rust.Bridge.Net;
public class ReferenceReviewMappingTests
{
 static ContractEditorIntent Input(FuturesOptionContractReadModel original)
 {
  var row=ReferenceReviewMapping.Row(new("same-id",new()),original);
  return new(1,Guid.NewGuid(),"option-contract","review",7,row.Id,row.Fields);
 }
 [Fact]public void Draft_review_changes_only_review_conventions_and_retains_exact_provider_facts()
 {
  var original=new FuturesOptionContractReadModel{SchemaVersion=1,ContractId="same-id",Dataset="GLBX.MDP3",InstrumentId=123,PublisherId=1,ReviewState=ReferenceReviewState.Draft,UnderlyingContractId="original-underlying",StrikePrice=6500.25,StrikePriceDecimal=6500.25m,DefinitionDigest=new string('a',64),RawDefinitionReference="immutable-reference"};
  var input=Input(original);input.Fields["evidenceId"]="Reviewed evidence";input.Fields["exchangeTimeZoneId"]="America/Chicago";input.Fields["strikePrice"]="9999";input.Fields["underlyingContractId"]="wrong-underlying";input.Fields["definitionDigest"]="wrong";
  Assert.Equal(original with{EvidenceId="Reviewed evidence",ExchangeTimeZoneId="America/Chicago",CalendarVersion="",MappingVersion="",TickRuleVersion=""},ReferenceReviewMapping.Option(input,original));
 }
 [Fact]public void Reviewed_status_requires_the_existing_complete_qualification_and_rejects_unknown_enums()
 {
  var original=new FuturesOptionContractReadModel{SchemaVersion=1,ReviewState=ReferenceReviewState.Draft};var input=Input(original);input.Fields["reviewState"]="Reviewed";
  Assert.Contains("not qualified",Assert.Throws<ArgumentException>(()=>ReferenceReviewMapping.Option(input,original)).Message);
  input.Fields["reviewState"]="999";Assert.Throws<ArgumentException>(()=>ReferenceReviewMapping.Option(input,original));
  input.Fields["reviewState"]="Draft";input.Fields["lastTradingUtc"]="2026-12-18T10:00:00-05:00";Assert.Throws<ArgumentException>(()=>ReferenceReviewMapping.Option(input,original));
 }
 [Fact]public void Legacy_contract_requires_provider_import_before_review()
 {
  var original=new FuturesOptionContractReadModel();Assert.Throws<ArgumentException>(()=>ReferenceReviewMapping.Option(Input(original),original));
 }
}
