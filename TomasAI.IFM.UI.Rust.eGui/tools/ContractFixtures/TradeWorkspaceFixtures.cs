using System.Reflection;
using System.Runtime.CompilerServices;
using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.ViewModels.Trade;
using TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Calls the unchanged hybrid snapshot formatter over frozen models.
/// The session constructor/lifecycle is deliberately not invoked: this oracle
/// owns no service container, network connection, consumer or real trade.</summary>
internal static class TradeWorkspaceFixtures {
 public static async Task Export(string output){
  var contract=new FuturesContractV3ReadModel{ContractId="ESZ6",Symbol="ES",LastTradeDate=new(2026,12,18)};
  await using var model=new TradeOrderEditorViewModel(DispatchProxy.Create<IAppRoot,PortfolioFixtureProxy>(),new(2026,10,26),[contract],DispatchProxy.Create<IReferenceDataService,PortfolioFixtureProxy>(),brokerEnvironment:BrokerEnvironment.Emulator);
  var flags=BindingFlags.Instance|BindingFlags.NonPublic;
  void Field(object target,string name,object value)=>target.GetType().GetField(name,flags)!.SetValue(target,value);
  var fund=PortfolioFixtures.Fund;
  var order=new PortfolioFundOrderEditorModel(TradeWorkspaceResponder.Order);
  var trade=(PortfolioFundOrderTradeEditorModel)typeof(TradeOrderEditorViewModel).GetMethod("ToEditorTrade",flags)!.Invoke(model,[TradeWorkspaceResponder.Trade])!;
  order.Add(trade);
  Field(model,"_portfolios",new[]{PortfolioFixtures.Portfolio});
  Field(model,"_funds",new[]{new PortfolioFundEditorModel(2,fund.Name,fund.Objective,0,true,fund.CreatedOnUtc,fund.CreatedBy)});
  Field(model,"_fundOrders",new[]{order});Field(model,"_fundOrderTrades",new[]{trade});
  foreach(var field in new[]{"_portfolioSelectedIndex","_fundSelectedIndex","_fundOrderSelectedIndex","_fundOrderTradeSelectedIndex"})Field(model,field,0);
  var session=MessagePackSerializer.Deserialize<ServiceResult<MarketSessionReadModel>>(File.ReadAllBytes(Path.Combine(output,"market-session-result.wire.msgpack")),MessagePackBinarySerializer.Options).Value!;
  model.ApplyMarketSessionSnapshot(session);
  var type=typeof(ContractEditorIntent).Assembly.GetType("TomasAI.IFM.UI.Rust.Bridge.Net.TradeWorkspaceSession")!;
  var formatter=RuntimeHelpers.GetUninitializedObject(type);
  Field(formatter,"model",model);
  Field(formatter,"openTrades",Activator.CreateInstance(type.GetField("openTrades",flags)!.FieldType)!);
  Field(formatter,"apiValueDate",new DateOnly(2026,10,26));Field(formatter,"fromDate",new DateOnly(2026,10,1));Field(formatter,"toDate",new DateOnly(2026,10,31));
  var result=(ContractEditorResult)type.GetMethod("Snapshot",flags)!.Invoke(formatter,null)!;
  FixtureFiles.WriteAllBytes(Path.Combine(output,"trade-workspace-session.msgpack"),MessagePackSerializer.Serialize(result,MessagePackBinarySerializer.ContentOptions));
 }
}
