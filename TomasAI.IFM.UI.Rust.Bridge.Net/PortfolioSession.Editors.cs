using System.Globalization;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;

sealed partial class PortfolioSession
{
    PortfolioDialog Editor(string key,string title,int width,int height,int caption,PortfolioField[] fields,string save="Save")
    { var dialog=new PortfolioDialog(key,title,width,height,caption,fields,[save,"Cancel"]); dialogs.Add(dialog); return dialog; }
    async Task LoadCatalog()
    {
        var result=await services.ReferenceQueries.GetStrategyDeploymentChoicesAsync();
        if(!result.Success || result.Value is null) throw new InvalidOperationException(result.ErrorMessage ?? "Strategy deployments are unavailable.");
        if(result.Value.Any(d=>d.Key.Kind!=StrategyCatalogKind.Deployment||d.Key.Id==Guid.Empty||d.Key.Version<=0)||result.Value.Select(d=>d.Key).Distinct().Count()!=result.Value.Length)throw new InvalidOperationException("Invalid or duplicate ConfigurationDb deployment identities.");
        deployments=result.Value.GroupBy(d=>d.Key.Id).Select(g=>g.MaxBy(d=>d.Key.Version)!).Where(d=>d.Status!=CatalogLifecycleStatus.Retired).OrderBy(d=>d.Code,StringComparer.Ordinal).ToArray();
    }
    async Task Open(string view)
    {
        if(view is "version-fund" or "fund-state" or "allocation" or "envelope" or "assignment" && !IsCurrentFund)
            throw new InvalidOperationException("Select the latest Fund version before changing its configuration or state.");
        editMode=view; message="";
        switch(view) {
            case "new-portfolio": case "version-portfolio": {
                int id; PortfolioReadModel value;
                if(view=="new-portfolio") {
                    var allocation=await model.AllocatePortfolioIdAsync(stopping.Token);
                    if(!allocation.IsSuccessful || allocation.PortfolioId is null) throw new InvalidOperationException(allocation.Error);
                    id=allocation.PortfolioId.Id;
                    value=new() { PortfolioId=id, PortfolioVersion=1, BaseCurrency="USD", OperatingState=PortfolioOperatingState.Draft,EffectiveFromUtc=DateTime.UtcNow,CreatedOnUtc=DateTime.UtcNow,CreatedBy=Environment.UserName };
                } else { id=Portfolio.PortfolioId; value=Portfolio with { PortfolioVersion=checked(Portfolio.PortfolioVersion+1),EffectiveUntilUtc=null,CreatedOnUtc=DateTime.UtcNow,CreatedBy=Environment.UserName }; }
                editing=value;
                Editor(view,view=="new-portfolio"?"Create Portfolio":"Create Portfolio Version",640,260,180,[
                    Field("PortfolioId","Portfolio ID",id,enabled:false),Field("Name","Name",value.Name),Field("BaseCurrency","Base Currency",value.BaseCurrency,"choice",choices:Enum.GetValues<TomasAI.IFM.Domain.MarketData.Shared.CurrencyType>().Select(currency=>new PortfolioChoice(currency.ToString(),currency.ToString())).ToArray()),
                    Field("OperatingState","Operating State",value.OperatingState,"choice",choices:EnumChoices<PortfolioOperatingState>()),Field("BrokerAccountRefs","Broker Accounts (CSV)",string.Join(", ",value.BrokerAccountRefs)),Field("EffectiveFromUtc","Effective From",value.EffectiveFromUtc,"datetime")]); break;
            }
            case "portfolio-state": Editor(view,"Change Portfolio State",520,150,140,[Field("state","Target State",Portfolio.OperatingState,"choice",choices:EnumChoices<PortfolioOperatingState>()),Field("reason","Reason","")],"Apply"); break;
            case "fund-state": Editor(view,"Change Fund State",520,150,140,[Field("state","Target State",Fund.OperatingState,"choice",choices:EnumChoices<FundOperatingState>()),Field("reason","Reason","")],"Apply"); break;
            case "delete-portfolio":
                if(Portfolio.OperatingState!=PortfolioOperatingState.Draft) throw new InvalidOperationException("Only a Draft Portfolio can be deleted.");
                Editor(view,"Delete Draft Portfolio",660,320,190,[Field("warning","",$"Delete Draft Portfolio {Portfolio.PortfolioId}: {Portfolio.Name}. The sequence-generated ID will never be reused. Type '{Portfolio.PortfolioId}' to confirm.","label",false),Field("confirmation","Portfolio ID",""),Field("reason","Reason","")],"Delete Draft"); break;
            case "new-fund": case "version-fund": {
                await LoadCatalog(); selections=await FundSelectionCatalog.LoadAsync(services.ReferenceQueries,stopping.Token);
                var id=view=="new-fund"?await model.AllocateFundIdAsync(stopping.Token):Fund.FundId;
                if(id is null) throw new InvalidOperationException(model.Message);
                var value=view=="new-fund"?new FundMandateReadModel { PortfolioId=Portfolio.PortfolioId,FundId=id.Value,FundCode="FUND-"+id,FundMandateVersion=1,TradingYear=DateTime.UtcNow.Year,OperatingState=FundOperatingState.Draft,DecisionHorizon="Daily",EffectiveFromUtc=DateTime.UtcNow,CreatedOnUtc=DateTime.UtcNow,CreatedBy=Environment.UserName }:
                    Fund with { FundMandateVersion=checked(Fund.FundMandateVersion+1),EffectiveUntilUtc=null,EffectiveFromUtc=DateTime.UtcNow,CreatedOnUtc=DateTime.UtcNow,CreatedBy=Environment.UserName };
                editing=value;
                PortfolioChoice[] Lookup(IEnumerable<LookupDefinitionReadModel> rows)=>rows.Select(r=>new PortfolioChoice(r.InternalValue,r.DisplayName,FundSelectionCatalog.IsSelectable(r))).ToArray();
                var familyChoices=deployments.Select(d=>new PortfolioChoice(d.Key.Id+"@"+d.Key.Version,$"{d.Name} [{d.Symbol} / {d.Currency} / {d.TimeFrame} {d.Exchange}] v{d.Key.Version} {d.Status}")).ToList();
                if(value.PermittedTradeStrategyFamilies.Length==0)foreach(var legacy in value.PermittedTradeFamilies)familyChoices.Add(new("unavailable:legacy:"+legacy,$"Unavailable: {legacy} — uncheck to remove"));
                foreach(var legacy in value.PermittedTradeStrategyFamilies.Where(r=>!deployments.Any(d=>r==TradeStrategyFamilyReference.From(d)))) familyChoices.Add(new("unavailable:"+familyChoices.Count,$"Unavailable: {legacy.CatalogDeployment?.Id.ToString()??legacy.TradeStrategyFamilyId.ToString()} — uncheck to remove"));
                var selected=value.PermittedTradeStrategyFamilies.Select(r=>deployments.FirstOrDefault(d=>r==TradeStrategyFamilyReference.From(d))).Where(d=>d is not null).Select(d=>d!.Key.Id+"@"+d.Key.Version).Concat(familyChoices.Where(c=>c.Value.StartsWith("unavailable:",StringComparison.Ordinal)).Select(c=>c.Value));
                Editor(view,view=="new-fund"?"Create Fund":"Change Fund",780,560,225,[Field("FundId","Fund ID",id,"text",false),Field("Name","Name",value.Name),Field("TradingYear","Trading Year",value.TradingYear,"number",min:2000,max:2200,decimals:0),
                    Field("OperatingState","Operating State",value.OperatingState,"choice",view!="new-fund",EnumChoices<FundOperatingState>()),Field("DecisionHorizon","Decision Horizon",value.DecisionHorizon,"choice",choices:TradeStrategyTimeFrames.Allowed.Select(t=>new PortfolioChoice(t.ToString(),t.ToString())).ToArray()),Field("Objective","Objective",value.Objective),
                    Field("UnderlyingUniverse","Underlyings",string.Join(",",value.UnderlyingUniverse),"multi",choices:selections.Underlyings.Select(x=>new PortfolioChoice(x,x)).ToArray()),Field("EligibleAssetTypes","Asset Types",string.Join(",",value.EligibleAssetTypes),"multi",choices:Lookup(selections.AssetTypes)),
                    Field("PermittedDirections","Directions",string.Join(",",value.PermittedDirections),"multi",choices:Lookup(selections.Directions)),Field("PermittedConditions","Market Conditions",string.Join(",",value.PermittedConditions),"multi",choices:Lookup(selections.MarketConditions)),Field("families","Permitted Strategy Deployments",string.Join(",",selected),"checklist",choices:familyChoices.ToArray())]);
                dialogs[^1]=dialogs[^1] with {Fields=dialogs[^1].Fields.Select(f=>f.Kind=="multi"?f with {Choices=[..f.Choices??[],..Csv(f.Value).Where(v=>f.Choices?.Any(c=>c.Value==v)!=true).Select(v=>new PortfolioChoice(v,$"Unavailable: {v}",false))]}:f).ToArray()};
                break;
            }
            case "allocation": {
                var value=model.Allocation ?? new FundAllocationReadModel { TargetWeight=.5m,MaximumWeight=1, Currency=Portfolio.BaseCurrency,SourcePolicyVersion=Math.Max(1,Portfolio.ActivePolicyVersion) };
                editing=value;
                Editor(view,"Fund Allocation",640,280,225,[Field("TargetWeight","Target Weight",value.TargetWeight,"number",max:1,decimals:4),Field("MinimumWeight","Minimum Weight",value.MinimumWeight,"number",max:1,decimals:4),Field("MaximumWeight","Maximum Weight",value.MaximumWeight,"number",max:1,decimals:4),Field("AllocatedCapital","Allocated Capital",value.AllocatedCapital,"number"),Field("Currency","Currency",value.Currency),Field("SourcePolicyVersion","Source Policy Version",value.SourcePolicyVersion,"number",min:1,max:long.MaxValue,decimals:0)]); break;
            }
            case "envelope": {
                var value=model.RiskEnvelope ?? new FundRiskEnvelopeReadModel { CapacityState=FundCapacityState.Available,Currency=Portfolio.BaseCurrency };
                editing=value;
                Editor(view,"Fund Risk Envelope",780,450,225,[Field("CapacityState","Capacity State",value.CapacityState,"choice",choices:EnumChoices<FundCapacityState>()),Field("Currency","Currency",value.Currency),Field("AllocatedCapital","Allocated Capital",value.AllocatedCapital,"number"),Field("AvailableCapital","Available Capital",value.AvailableCapital,"number"),Field("MaximumRiskPerTrade","Maximum Risk / Trade",value.MaximumRiskPerTrade,"number"),Field("MaximumAggregateRisk","Maximum Aggregate Risk",value.MaximumAggregateRisk,"number"),Field("MaximumMargin","Maximum Margin",value.MaximumMargin,"number"),Field("MaximumGrossNotional","Maximum Gross Notional",value.MaximumGrossNotional,"number"),Field("MaximumContracts","Maximum Contracts",value.MaximumContracts,"number",max:int.MaxValue,decimals:0),Field("MaximumOpenPositions","Maximum Open Positions",value.MaximumOpenPositions,"number",max:int.MaxValue,decimals:0),Field("RemainingLossBudget","Remaining Loss Budget",value.RemainingLossBudget,"number"),Field("days","Effective Days",30,"number",min:1,max:3650,decimals:0)]); break;
            }
            case "assignment":
                await LoadCatalog();
                if(deployments.Length==0)throw new InvalidOperationException("No strategy deployments are configured. Configure a deployment in Reference Data before assigning it to a Fund.");
                Editor(view,"Trade Template Assignment",780,450,225,[Field("Enabled","Enable assignment",false,"check",false),Field("TradeTemplateId","Deployment ID","","text",false),Field("TradeTemplateVersion","Template Version",1,"number",false),Field("DecisionHorizon","Decision Horizon",Fund.DecisionHorizon,"choice",false,TradeStrategyTimeFrames.Allowed.Select(t=>new PortfolioChoice(t.ToString(),t.ToString())).ToArray()),Field("UnderlyingUniverse","Underlyings (CSV)",string.Join(", ",Fund.UnderlyingUniverse),"text",false),Field("AssetType","Asset Type",Fund.EligibleAssetTypes.FirstOrDefault(),"text",false),Field("deployment","Trade Family","","choice",choices:deployments.Where(d=>Fund.PermittedTradeStrategyFamilies.Contains(TradeStrategyFamilyReference.From(d))).Select(d=>new PortfolioChoice(d.Key.Id+"@"+d.Key.Version,$"{d.Name} [{d.Symbol} / {d.Currency} / {d.TimeFrame} {d.Exchange}] v{d.Key.Version} {d.Status}")).ToArray()),Field("Priority","Priority",0,"number",max:int.MaxValue,decimals:0),Field("TradeSelectionHintProfileId","Selection Hint Profile ID","","text",false),Field("TradeSelectionHintProfileVersion","Hint Profile Version",0,"number",false),Field("OrderCompositionProfileId","Composition Profile ID","","text",false),Field("OrderCompositionProfileVersion","Composition Version",0,"number",false)]);
                var available=dialogs[^1].Fields.Single(f=>f.Key=="deployment").Choices??[];
                if(available.Length==1)await Choose(new(1,Guid.NewGuid(),"portfolio","portfolio-choice",0,null,new(){["deployment"]=available[0].Value}));
                else if(available.Length==0)dialogs[^1]=dialogs[^1] with {Status="No active catalog family is permitted by this Fund. Update its mandate before assigning a strategy."};
                break;
            case "risk-policy": inlinePolicyMode=false; await OpenPolicies(); break;
            case "financials": await OpenFinancials(); break;
            case "risk-history": await OpenRiskHistory(); break;
            default: await OpenFinancialEditor(view); break;
        }
    }
    async Task Choose(ContractEditorIntent input)
    {
        if(dialogs.LastOrDefault()?.Key=="assignment") {
            var d=deployments.Single(d=>d.Key.Id+"@"+d.Key.Version==F(input,"deployment"));
            if(!Fund.PermittedTradeStrategyFamilies.Contains(TradeStrategyFamilyReference.From(d))) throw new ArgumentException("Deployment is not permitted by the Fund.");
            var hint=d.PipelineParameters.SingleOrDefault(p=>p.Kind==CatalogPipelineParameterKind.TradeSelection); var composition=d.PipelineParameters.SingleOrDefault(p=>p.Kind==CatalogPipelineParameterKind.OrderComposition);
            var values=new Dictionary<string,string> { ["deployment"]=F(input,"deployment"),["TradeTemplateId"]=d.Key.Id.ToString(),["TradeTemplateVersion"]=d.Key.Version.ToString(CultureInfo.InvariantCulture),["DecisionHorizon"]=d.Horizon.ToString(),["UnderlyingUniverse"]=string.Join(",",d.Products.Select(p=>p.Symbol).Distinct()),["AssetType"]=d.InstrumentClasses.FirstOrDefault(x=>Fund.EligibleAssetTypes.Contains(x,StringComparer.Ordinal))??"",["TradeSelectionHintProfileId"]=hint?.Id.ToString()??"",["TradeSelectionHintProfileVersion"]=(hint?.Version??0).ToString(CultureInfo.InvariantCulture),["OrderCompositionProfileId"]=composition?.Id.ToString()??"",["OrderCompositionProfileVersion"]=(composition?.Version??0).ToString(CultureInfo.InvariantCulture),["Enabled"]="false" };
            dialogs[^1]=dialogs[^1] with {Fields=dialogs[^1].Fields.Select(f=>values.TryGetValue(f.Key,out var v)?f with {Value=v,Enabled=f.Key=="Enabled"?d.Status==CatalogLifecycleStatus.Published&&hint is not null&&composition is not null:f.Enabled}:f).ToArray()};
        } else await ChooseFinancial(input);
    }
    async Task Save(ContractEditorIntent input)
    {
        if(dialogs.Count==0) throw new InvalidOperationException("No editor is open.");
        var key=dialogs[^1].Key; bool saved;
        switch(key) {
            case "new-portfolio": case "version-portfolio": {
                var value=Patch((PortfolioReadModel)editing!,input); value=value with { PortfolioId=((PortfolioReadModel)editing!).PortfolioId,PortfolioVersion=((PortfolioReadModel)editing!).PortfolioVersion,BaseCurrency=value.BaseCurrency.Trim().ToUpperInvariant(),Name=value.Name.Trim() };
                Valid(value.Validate(requireActivePolicy:value.OperatingState==PortfolioOperatingState.Active));
                saved=key=="new-portfolio"?await model.CreatePortfolioAsync(value,stopping.Token):await model.AddPortfolioVersionAsync(value,Portfolio.PortfolioVersion,stopping.Token); if(saved)filter=value.OperatingState; break;
            }
            case "portfolio-state": case "fund-state":
                if(string.IsNullOrWhiteSpace(F(input,"reason")))throw new ArgumentException("Enter a reason.");
                saved=key=="portfolio-state"?await model.ChangePortfolioStateAsync(Enum.Parse<PortfolioOperatingState>(F(input,"state")),F(input,"reason").Trim(),stopping.Token):await model.ChangeFundStateAsync(Enum.Parse<FundOperatingState>(F(input,"state")),F(input,"reason").Trim(),stopping.Token); if(saved&&key=="portfolio-state")filter=Enum.Parse<PortfolioOperatingState>(F(input,"state")); break;
            case "delete-portfolio":
                if(F(input,"confirmation")!=Portfolio.PortfolioId.ToString(CultureInfo.InvariantCulture)||string.IsNullOrWhiteSpace(F(input,"reason")))throw new ArgumentException("Type the Portfolio ID and a deletion reason.");
                saved=await model.DeleteDraftPortfolioAsync(F(input,"reason").Trim(),stopping.Token); break;
            case "new-fund": case "version-fund": {
                var value=Patch((FundMandateReadModel)editing!,input);
                value=value with {Name=value.Name.Trim(),Objective=value.Objective.Trim(),PortfolioId=Portfolio.PortfolioId,FundId=((FundMandateReadModel)editing!).FundId,FundMandateVersion=((FundMandateReadModel)editing!).FundMandateVersion,OperatingState=key=="new-fund"?FundOperatingState.Draft:value.OperatingState};
                selections!.ValidateSelections(value.UnderlyingUniverse,value.EligibleAssetTypes,value.PermittedDirections,value.PermittedConditions);
                var families=Csv(F(input,"families"));
                var selected=families.Select(k=>deployments.SingleOrDefault(d=>d.Key.Id+"@"+d.Key.Version==k)??throw new ArgumentException("Remove unavailable strategy deployments before saving.")).ToArray();
                value=value with {SchemaVersion=3,PermittedTradeStrategyFamilies=selected.Select(TradeStrategyFamilyReference.From).ToArray(),PermittedTradeFamilies=selected.Select(d=>d.SystemKey).Distinct(StringComparer.Ordinal).ToArray()}; Valid(value.Validate());
                saved=key=="new-fund"?await model.CreateFundAsync(value,stopping.Token):await model.AddFundVersionAsync(value,Fund.FundMandateVersion,stopping.Token); break;
            }
            case "allocation": {
                var v=Patch((FundAllocationReadModel)editing!,input); v=v with { PortfolioId=Portfolio.PortfolioId,PortfolioVersion=Portfolio.PortfolioVersion,FundId=Fund.FundId,FundMandateVersion=Fund.FundMandateVersion,AllocationVersion=(model.Allocation?.AllocationVersion??0)+1,SourcePolicyId=Portfolio.ActivePolicyId,Currency=v.Currency.Trim().ToUpperInvariant(),EffectiveFromUtc=DateTime.UtcNow,CreatedOnUtc=DateTime.UtcNow,CreatedBy=Environment.UserName }; Valid(v.Validate()); saved=await model.DelegateAllocationAsync(v,stopping.Token); break;
            }
            case "envelope": {
                var v=Patch((FundRiskEnvelopeReadModel)editing!,input); v=v with { PortfolioId=Portfolio.PortfolioId,PortfolioVersion=Portfolio.PortfolioVersion,FundId=Fund.FundId,FundMandateVersion=Fund.FundMandateVersion,EnvelopeId=model.RiskEnvelope?.EnvelopeId??Guid.NewGuid(),EnvelopeVersion=(model.RiskEnvelope?.EnvelopeVersion??0)+1,Currency=v.Currency.Trim().ToUpperInvariant(),SourcePolicyId=Portfolio.ActivePolicyId,SourcePolicyVersion=Portfolio.ActivePolicyVersion,EffectiveFromUtc=DateTime.UtcNow,ExpiresAtUtc=DateTime.UtcNow.AddDays((double)N(input,"days")),CreatedOnUtc=DateTime.UtcNow,CreatedBy=Environment.UserName }; Valid(v.Validate()); saved=await model.DelegateRiskEnvelopeAsync(v,stopping.Token); break;
            }
            case "assignment": {
                var d=deployments.Single(d=>d.Key.Id+"@"+d.Key.Version==F(input,"deployment"));
                if(!Fund.PermittedTradeStrategyFamilies.Contains(TradeStrategyFamilyReference.From(d)))throw new ArgumentException("Select a permitted deployment.");
                var hint=d.PipelineParameters.SingleOrDefault(p=>p.Kind==CatalogPipelineParameterKind.TradeSelection); var composition=d.PipelineParameters.SingleOrDefault(p=>p.Kind==CatalogPipelineParameterKind.OrderComposition);
                var v=new FundTradeTemplateAssignmentReadModel { SchemaVersion=3,PortfolioId=Portfolio.PortfolioId,PortfolioVersion=Portfolio.PortfolioVersion,FundId=Fund.FundId,FundMandateVersion=Fund.FundMandateVersion,AssignmentVersion=1,TradeTemplateId=d.Key.Id,TradeTemplateVersion=d.Key.Version,TradeStrategyFamily=TradeStrategyFamilyReference.From(d),TradeFamily=d.SystemKey,Enabled=B(input,"Enabled"),DecisionHorizon=d.Horizon.ToString(),UnderlyingUniverse=d.Products.Select(p=>p.Symbol).Distinct().ToArray(),AssetType=d.InstrumentClasses.FirstOrDefault(x=>Fund.EligibleAssetTypes.Contains(x,StringComparer.Ordinal))??"",Priority=(int)N(input,"Priority"),EffectiveFromUtc=DateTime.UtcNow,TradeSelectionHintProfileId=hint?.Id??Guid.Empty,TradeSelectionHintProfileVersion=hint?.Version??0,OrderCompositionProfileId=composition?.Id??Guid.Empty,OrderCompositionProfileVersion=composition?.Version??0,CreatedOnUtc=DateTime.UtcNow,CreatedBy=Environment.UserName }; Valid(v.Validate()); saved=await model.AssignTradeTemplateAsync(v,stopping.Token); break;
            }
            default: await SaveFinancial(input); return;
        }
        message=model.Message;
        if(!saved)throw new InvalidOperationException(message);
        dialogs.RemoveAt(dialogs.Count-1); editing=null; await Refresh();
    }
}
