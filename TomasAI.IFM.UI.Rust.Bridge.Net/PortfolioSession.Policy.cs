using System.Globalization;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.UI.Net.ViewModels.Strategy;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;

sealed partial class PortfolioSession
{
    PortfolioDialog? inlinePolicy;
    bool inlinePolicyMode;
    async Task LoadInlinePolicy()
    {
        inlinePolicy=null;
        if(model.SelectedPortfolio is null)return;
        inlinePolicyMode=true;
        try {await OpenPolicies();}
        catch(Exception error) when(error is not OperationCanceledException){message=error.Message;}
    }
    PortfolioFinancialPolicyReadModel[] policies=[];
    PortfolioFinancialPolicyReadModel? selectedPolicy, editingPolicy;
    bool policyVersion;
    bool policyDirty;
    RiskHistoryRow[] riskRows=[];
    string? riskCursor;
    DateOnly riskDate=DateOnly.FromDateTime(DateTime.UtcNow);
    static readonly (string Key,string Caption)[] PolicyFields=[("Name","Name"),("BaseCurrency","Base Currency"),("CapitalBase","Capital Base"),("ProtectedReserve","Protected Reserve"),("MaximumDeployableCapital","Deployable Capital"),("MaximumRiskPerTrade","Risk Per Trade"),("MaximumAggregateRisk","Aggregate Risk"),("MaximumMargin","Maximum Margin"),("MaximumGrossNotional","Gross Notional"),("MaximumOpenPositions","Open Positions"),("MaximumDrawdownAmount","Drawdown")];
    async Task OpenPolicies()
    {
        await LoadCatalog();
        var result=await services.PortfolioQueries.GetPoliciesAsync(Portfolio.PortfolioId,200,cancellationToken:stopping.Token);
        if(!result.Success || result.Value is null)throw new InvalidOperationException(result.ErrorMessage);
        policies=result.Value.Items;selectedPolicy=policies.FirstOrDefault(p=>p.PolicyId==Portfolio.ActivePolicyId&&p.PolicyVersion==Portfolio.ActivePolicyVersion)??policies.FirstOrDefault(p=>p.PolicyId==selectedPolicy?.PolicyId && p.PolicyVersion==selectedPolicy?.PolicyVersion)??policies.FirstOrDefault();
        editingPolicy=null;
        policyDirty=false;
        UpdatePolicyDialog();
    }
    void UpdatePolicyDialog()
    {
        var source=editingPolicy??selectedPolicy;
        var values=source is null?new Dictionary<string,string>():BridgeDocument.Fields(source,enumNames:true);
        var fields=PolicyFields.Select(f=>Field(f.Key,f.Caption,values.GetValueOrDefault(f.Key),f.Key is "Name" or "BaseCurrency"?"text":"number",editingPolicy is not null&&f.Key!="BaseCurrency")).ToArray();
        var grids=new[] { Grid("policies",policies,selected:selectedPolicy is null?null:$"{selectedPolicy.PolicyId}@{selectedPolicy.PolicyVersion}",identity:o=>{var p=(PortfolioFinancialPolicyReadModel)o;return $"{p.PolicyId}@{p.PolicyVersion}";}),Grid("family-limits",source?.TradeFamilyLimits??[]) with {Editable=editingPolicy is not null} };
        var limits=grids[1];
        grids[1]=limits with {Columns=limits.Columns.Where(c=>c is not ("DefinitionVersion" or "CatalogDeployment")).ToArray(),Rows=limits.Rows.Select((r,i)=>{
            var values=new Dictionary<string,string>(r.Fields);
            var limit=source!.TradeFamilyLimits[i];
            values["TradeStrategyFamilyId"]=limit.CatalogDeployment is {} key?deployments.FirstOrDefault(d=>d.Key==key)?.Description??$"Unavailable deployment {key.Id} v{key.Version}":$"Legacy family {limit.TradeStrategyFamilyId} v{limit.DefinitionVersion}";
            return r with {Fields=values};
        }).ToArray()};
        var d=new PortfolioDialog("risk-policy","Portfolio Risk Policy",1260,820,170,fields,["New Policy","New Version","Save","Cancel","Activate && Assign","Retire","Delete Draft"],$"Portfolio {Portfolio.PortfolioId} — {Portfolio.Name}; Active policy: {Portfolio.ActivePolicyId} v{Portfolio.ActivePolicyVersion}",grids);
        d=d with {ButtonEnabled=new(){["New Policy"]=editingPolicy is null,["Change Policy..."]=editingPolicy is null&&selectedPolicy is not null,["New Version"]=editingPolicy is null&&selectedPolicy is not null,["Save"]=editingPolicy is not null,["Cancel"]=editingPolicy is not null,["Activate && Assign"]=editingPolicy is null&&selectedPolicy?.OperatingState==PortfolioFinancialPolicyState.Draft,["Retire"]=editingPolicy is null&&selectedPolicy is not null,["Delete Draft"]=editingPolicy is null&&selectedPolicy?.OperatingState==PortfolioFinancialPolicyState.Draft}};
        if(inlinePolicyMode){inlinePolicy=d with {Status="",Buttons=d.Buttons.Take(1).Concat(["Change Policy..."]).Concat(d.Buttons.Skip(1)).ToArray()};return;}
        var index=dialogs.FindIndex(d=>d.Key=="risk-policy");if(index<0)dialogs.Add(d);else dialogs[index]=d;
    }
    async Task PolicyAction(ContractEditorIntent input)
    {
        message="";
        var action=F(input,"action");
        if(dialogs.LastOrDefault()?.Key=="discard-policy") {
            dialogs.RemoveAt(dialogs.Count-1);
            if(action=="Yes"){editingPolicy=null;dialogs.RemoveAt(dialogs.Count-1);}
            return;
        }
        switch(action) {
            case "New Policy": {
                policyDirty=false;
                var allocated=await services.PortfolioIdentities.AllocatePolicyIdAsync(stopping.Token);
                if(!allocated.Success||allocated.Value is null)throw new InvalidOperationException(allocated.ErrorMessage);
                editingPolicy=new() { PortfolioId=Portfolio.PortfolioId,PolicyId=allocated.Value.Value,PolicyVersion=1,SchemaVersion=3,OperatingState=PortfolioFinancialPolicyState.Draft,BaseCurrency=Portfolio.BaseCurrency,EffectiveFromUtc=DateTime.UtcNow,CreatedOnUtc=DateTime.UtcNow,CreatedBy=Environment.UserName,TradeFamilyLimits=deployments.Select(d=>new TradeFamilyRiskLimitReadModel {CatalogDeployment=d.Key}).ToArray() };policyVersion=false; break;
            }
            case "Change Policy...":
            case "New Version":
                policyDirty=false;
                if(selectedPolicy is null)throw new InvalidOperationException("Select a policy.");
                editingPolicy=selectedPolicy.DefensiveCopy() with { PolicyVersion=selectedPolicy.PolicyVersion+1,SchemaVersion=3,OperatingState=PortfolioFinancialPolicyState.Draft,EffectiveFromUtc=DateTime.UtcNow,EffectiveUntilUtc=null,CreatedOnUtc=DateTime.UtcNow,CreatedBy=Environment.UserName,SupersededOnUtc=null,SupersededBy="",TradeFamilyLimits=deployments.Select(d=>selectedPolicy.TradeFamilyLimits.SingleOrDefault(l=>l.CatalogDeployment==d.Key)??new TradeFamilyRiskLimitReadModel {CatalogDeployment=d.Key}).ToArray() };policyVersion=true; break;
            case "Cancel": editingPolicy=null;policyDirty=false;break;
            case "Save": {
                policyDirty=true;
                if(editingPolicy is null)throw new InvalidOperationException("No policy draft is being edited.");
                var value=Patch(editingPolicy,input);
                var limits=value.TradeFamilyLimits.Select((limit,index)=>Patch(limit,input with {Fields=input.Fields.Where(p=>p.Key.StartsWith($"limit.{index}.",StringComparison.Ordinal)).ToDictionary(p=>p.Key[($"limit.{index}.").Length..],p=>p.Value)})).ToArray();
                value=value with {Name=value.Name.Trim(),BaseCurrency=Portfolio.BaseCurrency,TradeFamilyLimits=limits};Valid(value.Validate());
                var result=policyVersion?await services.PortfolioPolicyCommands.AddPolicyVersionAsync(value,selectedPolicy!.AggregateRevision,stopping.Token):await services.PortfolioPolicyCommands.CreatePolicyAsync(value,Guid.NewGuid(),stopping.Token);
                if(!result.Success)throw new InvalidOperationException(result.ErrorMessage);await OpenPolicies();return;
            }
            case "Activate && Assign": {
                if(selectedPolicy is not {OperatingState:PortfolioFinancialPolicyState.Draft} p)throw new InvalidOperationException("Select a Draft policy.");
                var revision=await services.PortfolioQueries.GetPortfolioRevisionAsync(Portfolio.PortfolioId,stopping.Token);
                if(!revision.Success||revision.Value is null)throw new InvalidOperationException(revision.ErrorMessage);
                var result=await services.PortfolioPolicyCommands.ActivateAndAssignAsync(new(Portfolio.PortfolioId,p.PolicyId),p.AggregateRevision,p.PolicyVersion,revision.Value.Revision,stopping.Token);
                if(!result.Success)throw new InvalidOperationException(result.ErrorMessage);
                if(inlinePolicyMode)await Refresh();else await OpenPolicies();return;
            }
            case "Retire": {
                var p=selectedPolicy??throw new InvalidOperationException("Select a policy.");
                var result=await services.PortfolioPolicyCommands.RetirePolicyAsync(new(Portfolio.PortfolioId,p.PolicyId),p.AggregateRevision,p.PolicyVersion,"Operator retirement",stopping.Token);
                if(!result.Success)throw new InvalidOperationException(result.ErrorMessage);await OpenPolicies();return;
            }
            case "Delete Draft": {
                var p=selectedPolicy is {OperatingState:PortfolioFinancialPolicyState.Draft}?selectedPolicy:throw new InvalidOperationException("Only a Draft policy can be deleted.");
                var result=await services.PortfolioPolicyCommands.DeleteDraftPolicyAsync(new(Portfolio.PortfolioId,p.PolicyId),p.AggregateRevision,"Operator deleted unused Draft",stopping.Token);
                if(!result.Success)throw new InvalidOperationException(result.ErrorMessage);await OpenPolicies();return;
            }
        }
        UpdatePolicyDialog();
    }
    async Task OpenRiskHistory(bool next=false)
    {
        var response=await services.RiskQueries.GetHistoryAsync(Portfolio.PortfolioId,Fund.FundId,riskDate,25,next?riskCursor:null,stopping.Token);
        if(!response.Success||response.Value is null)throw new InvalidOperationException(response.ErrorMessage);
        riskRows=response.Value.Items;riskCursor=response.Value.PagingState;
        var d=new PortfolioDialog("risk-history",$"Risk history - Portfolio {Portfolio.PortfolioId}, Fund {Fund.FundId}",1300,850,160,[Field("date","Evaluation date (UTC)",riskDate.ToString("yyyy-MM-dd"),"date"),Field("decision","Decision and sizing","","multiline",false),Field("evidence","Evidence","","multiline",false)],["Refresh","Next page"],riskRows.Length==0?"No projected Risk history for this UTC date.":"Double-click an invocation to refresh its details and current authority.",[Grid("risk-history",riskRows,["EvaluatedAtUtc","Horizon","Variant","Outcome","Units","Reason","OrderId","WorkflowId","InvocationId"],identity:o=>((RiskHistoryRow)o).InvocationId.ToString())]);
        d=d with {ButtonEnabled=new(){["Refresh"]=true,["Next page"]=riskCursor is not null}};
        var index=dialogs.FindIndex(d=>d.Key=="risk-history");if(index<0)dialogs.Add(d);else dialogs[index]=d;
    }
    async Task GridAction(ContractEditorIntent input)
    {
        switch(F(input,"grid")) {
            case "policies":
                if(editingPolicy is not null)throw new InvalidOperationException("Save or cancel the edited policy before selecting another.");
                selectedPolicy=policies.Single(p=>$"{p.PolicyId}@{p.PolicyVersion}"==input.OriginalId);UpdatePolicyDialog();break;
            case "risk-history": {
                var row=riskRows.Single(r=>r.InvocationId.ToString()==input.OriginalId);
                var response=await services.RiskQueries.GetInvocationAsync(new(row.WorkflowId),row.InvocationId,stopping.Token);
                if(!response.Success||response.Value is null)throw new InvalidOperationException(response.ErrorMessage);
                dialogs[^1]=dialogs[^1] with {Fields=dialogs[^1].Fields.Select(f=>f.Key=="decision"?f with {Value=RiskObservationPresentation.Render(response.Value)}:f.Key=="evidence"?f with {Value=System.Text.Json.JsonSerializer.Serialize(response.Value,new System.Text.Json.JsonSerializerOptions{WriteIndented=true})}:f).ToArray()};break;
            }
            default: await FinancialGrid(input);break;
        }
    }
}
