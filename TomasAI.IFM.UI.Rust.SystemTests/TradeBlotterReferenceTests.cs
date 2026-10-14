using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using System.Text.Json;
using TomasAI.IFM.UI.Net.Views.Trade;
using TomasAI.IFM.UI.Net.Models.Portfolio;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
public sealed class TradeBlotterReferenceTests
{
 [System.Runtime.InteropServices.DllImport("user32.dll")]static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 [Fact]public void Capture_original_blotter_controls_without_trading_actions(){
  SetThreadDpiAwarenessContext(new(-4));var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
  using var host=new DesktopViewTestHost(scope=>{
   SetThreadDpiAwarenessContext(new(-4));var fund=new PortfolioFundEditorModel(701,"ES Daily Development Fund","",0,false,DateTime.UtcNow,"test");var order=new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel{PortfolioId=101,FundId=701,OrderId=701,Status="Draft"});var trade=new PortfolioFundOrderTradeEditorModel{PortfolioId=101,FundId=701,OrderId=701,TradeId=1201,TradeType=TradeType.ShortIronCondor,TradeState=TradeState.NewTrade,TradeAction=TradeAction.Sell,EffectiveDate=new(2026,10,14),BaseContractId="ES20261218",BaseContractSymbol="ES",UnderlyingRoot="ES",PrimaryTrade=true};
   var view=new EsTradeBlotterControl(scope,fund,order,trade,101,false);var form=new System.Windows.Forms.Form{Text="WinForms Trade Blotter Reference",ClientSize=new(1880,900),StartPosition=System.Windows.Forms.FormStartPosition.CenterScreen,BackColor=System.Drawing.Color.Black,Font=new("Microsoft Sans Serif",10F),TopMost=true};form.Controls.Add(view);return(form,()=>view.CloseAsync().AsTask());
  });
  using var automation=new UIA3Automation();var window=automation.FromHandle(host.Handle).AsWindow();window.Focus();
  foreach(var tab in new[]{"Market Selection","Broker Trade","Order Fills"}){
   window.FindFirstDescendant(cf=>cf.ByName(tab).And(cf.ByControlType(ControlType.TabItem)))!.AsTabItem().Select();Thread.Sleep(500);
   var controls=window.FindAllDescendants().Where(e=>!string.IsNullOrEmpty(e.Properties.AutomationId.ValueOrDefault)).Select(e=>new{Id=e.Properties.AutomationId.ValueOrDefault,Name=e.Properties.Name.ValueOrDefault,Type=e.ControlType.ToString(),X=e.BoundingRectangle.X-window.BoundingRectangle.X,Y=e.BoundingRectangle.Y-window.BoundingRectangle.Y,Width=e.BoundingRectangle.Width,Height=e.BoundingRectangle.Height,Enabled=e.IsEnabled}).ToArray();
   File.WriteAllText(Path.Combine(output,"blotter-winforms-"+tab.Replace(" ","-")+".json"),JsonSerializer.Serialize(new{Window=window.BoundingRectangle.ToString(),Controls=controls},new JsonSerializerOptions{WriteIndented=true}));window.Capture().Save(Path.Combine(output,"blotter-winforms-"+tab.Replace(" ","-")+".png"));Assert.NotEmpty(controls);


  }
 }
 [Fact]public void Capture_blotter_in_the_real_editor_parent(){
  SetThreadDpiAwarenessContext(new(-4));var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
  using var host=new DesktopViewTestHost(scope=>{SetThreadDpiAwarenessContext(new(-4));TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel[] contracts=[];scope.Services.MarketDataQueries.ExecuteObservableAsync(api=>api.GetFuturesContractsAsync(rows=>contracts=rows)).GetAwaiter().GetResult();var reference=scope.Container.GetInstance<TomasAI.IFM.UI.Net.Services.Reference.IReferenceDataService>();var model=new TomasAI.IFM.UI.Net.ViewModels.Trade.TradeOrderEditorViewModel(scope,scope.ValueDates.ValueDate,contracts,reference,brokerEnvironment:BrokerEnvironment.Emulator);var form=new TradeOrderEditorForm(scope,reference);form.FitToTradeView(new(764,60,1912,1301));form.LoadViewModel(model);form.TopMost=true;return(form,()=>model.DisposeAsync().AsTask());});
  using var automation=new UIA3Automation();var window=automation.FromHandle(host.Handle).AsWindow();window.FindFirstDescendant(cf=>cf.ByName("Trade Blotter").And(cf.ByControlType(ControlType.TabItem)))!.AsTabItem().Select();var watch=System.Diagnostics.Stopwatch.StartNew();while(window.FindFirstDescendant(cf=>cf.ByAutomationId("threeTabTradeBlotterLayout")) is null&&watch.Elapsed<TimeSpan.FromSeconds(20))Thread.Sleep(100);
  foreach(var tab in new[]{"Market Selection","Broker Trade","Order Fills"}){window.FindFirstDescendant(cf=>cf.ByName(tab).And(cf.ByControlType(ControlType.TabItem)))!.AsTabItem().Select();Thread.Sleep(300);
  var controls=window.FindAllDescendants().Where(e=>!string.IsNullOrEmpty(e.Properties.AutomationId.ValueOrDefault)).Select(e=>new{Id=e.Properties.AutomationId.ValueOrDefault,Name=e.Properties.Name.ValueOrDefault,Type=e.ControlType.ToString(),X=e.BoundingRectangle.X-window.BoundingRectangle.X,Y=e.BoundingRectangle.Y-window.BoundingRectangle.Y,Width=e.BoundingRectangle.Width,Height=e.BoundingRectangle.Height}).ToArray();
  File.WriteAllText(Path.Combine(output,"blotter-winforms-parent-"+tab.Replace(" ","-")+".json"),JsonSerializer.Serialize(new{Window=window.BoundingRectangle.ToString(),Controls=controls},new JsonSerializerOptions{WriteIndented=true}));Assert.Contains(controls,x=>x.Id=="threeTabTradeBlotterLayout");
   if(tab=="Market Selection")File.Copy(Path.Combine(output,"blotter-winforms-parent-Market-Selection.json"),Path.Combine(output,"blotter-winforms-parent.json"),true);
  }

 }

 [Fact]public void Frozen_emulator_chain_automatically_selects_the_strategy_with_live_feed_off(){
  SetThreadDpiAwarenessContext(new(-4));EsTradeBlotterControl? view=null;
  using var host=new DesktopViewTestHost(scope=>{
   var fund=new PortfolioFundEditorModel(701,"ES Daily Development Fund","",0,false,DateTime.UtcNow,"test");
   var order=new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel{PortfolioId=101,FundId=701,OrderId=701,Status="Draft"});
   var trade=new PortfolioFundOrderTradeEditorModel{PortfolioId=101,FundId=701,OrderId=701,TradeId=1201,TradeType=TradeType.ShortIronCondor,TradeState=TradeState.NewTrade,TradeAction=TradeAction.Sell,EffectiveDate=scope.ValueDates.ValueDate,RequestedMaturityDate=scope.ValueDates.ValueDate.AddDays(60),BaseContractId="ES20261218",BaseContractSymbol="ES",UnderlyingRoot="ES",PrimaryTrade=true};
   view=new EsTradeBlotterControl(scope,fund,order,trade,101,false,liveFeedEnabled:false);
   var form=new System.Windows.Forms.Form{Text="WinForms Frozen Emulator Selection",ClientSize=new(1880,900),StartPosition=System.Windows.Forms.FormStartPosition.CenterScreen,BackColor=System.Drawing.Color.Black};form.Controls.Add(view);return(form,()=>view.CloseAsync().AsTask());
  });
  using var automation=new UIA3Automation();var window=automation.FromHandle(host.Handle).AsWindow();window.Focus();
  var watch=System.Diagnostics.Stopwatch.StartNew();bool selectedExpiry=false;int selected=0;string status="";
  while(watch.Elapsed<TimeSpan.FromSeconds(60)){
   (selected,status)=((int,string))view!.Invoke(new Func<(int,string)>(()=>{
    var expiry=(System.Windows.Forms.ComboBox)view.Controls.Find("expirationSelector",true).Single();
    if(!selectedExpiry){var items=expiry.Items.Cast<object>().Select((item,index)=>(Text:item.ToString(),Index:index)).ToArray();var target=items.FirstOrDefault(x=>x.Text=="20 Nov 26");if(target.Text is not null){selectedExpiry=true;expiry.SelectedIndex=target.Index;}}
    return(((System.Windows.Forms.DataGridView)view.Controls.Find("selectedStrategyLegsGrid",true).Single()).RowCount,view.Controls.Find("marketSelectionStatus",true).Single().Text);
   }));
   if(selectedExpiry&&selected==4)break;Thread.Sleep(200);
  }
  var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;
  File.WriteAllText(Path.Combine(output,"blotter-winforms-frozen-selection.json"),JsonSerializer.Serialize(new{selectedExpiry,selected,status,LiveFeed=false}));window.Capture().Save(Path.Combine(output,"blotter-winforms-frozen-selection.png"));
  Assert.True(selectedExpiry,"November 20 expiry was not available");Assert.Equal(4,selected);
 }

 [Fact]public void Capture_qualification_dialog_without_executing_account_actions(){
  SetThreadDpiAwarenessContext(new(-4));using var host=new DesktopViewTestHost(scope=>{var order=new PortfolioFundOrderEditorModel(new FundOrderProjectionReadModel{PortfolioId=101,FundId=701,OrderId=701,Status="Draft"});var trade=new PortfolioFundOrderTradeEditorModel{PortfolioId=101,FundId=701,OrderId=701,TradeId=1201,TradeType=TradeType.ShortIronCondor,TradeState=TradeState.NewTrade,TradeAction=TradeAction.Sell,EffectiveDate=new(2026,10,14),BaseContractId="ES20261218",BaseContractSymbol="ES",PrimaryTrade=true};TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel[] contracts=[];scope.Services.MarketDataQueries.ExecuteObservableAsync(api=>api.GetFuturesContractsAsync(rows=>contracts=rows)).GetAwaiter().GetResult();var model=new TomasAI.IFM.UI.Net.ViewModels.Trade.BrokerManualTradeOrderViewModel(scope,101,order,trade,contracts.Single(x=>x.ContractId=="ES20261218"));return(new BrokerAccountQualificationDialog(model),()=>Task.CompletedTask);});using var automation=new UIA3Automation();var dialog=automation.FromHandle(host.Handle).AsWindow();Thread.Sleep(500);var bounds=dialog.BoundingRectangle;var output=Environment.GetEnvironmentVariable("IFM_RUST_TEST_ARTIFACTS")!;File.WriteAllText(Path.Combine(output,"blotter-winforms-qualification.json"),JsonSerializer.Serialize(new{bounds.X,bounds.Y,bounds.Width,bounds.Height,Controls=dialog.FindAllDescendants().Where(e=>!string.IsNullOrEmpty(e.Properties.AutomationId.ValueOrDefault)).Select(e=>new{Id=e.Properties.AutomationId.ValueOrDefault,Name=e.Properties.Name.ValueOrDefault,X=e.BoundingRectangle.X-bounds.X,Y=e.BoundingRectangle.Y-bounds.Y,Width=e.BoundingRectangle.Width,Height=e.BoundingRectangle.Height})},new JsonSerializerOptions{WriteIndented=true}));Assert.True(bounds.Width>0);
 }

}
