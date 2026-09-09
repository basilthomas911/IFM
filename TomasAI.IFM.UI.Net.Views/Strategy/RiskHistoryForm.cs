using System.Text;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.UI.Net.Contracts;
namespace TomasAI.IFM.UI.Net.Views.Strategy;

/// <summary>Read-only Risk history. Every exact read refreshes current authorization independently of the historic calculation.</summary>
public sealed class RiskHistoryForm : DarkTradingForm
{
    readonly IRiskQueryApi _api;
    readonly int _portfolio, _fund;
    readonly DateTimePicker _date=new(){Format=DateTimePickerFormat.Short,Width=120,Value=DateTime.UtcNow.Date};
    readonly Button _refresh=new(){Text="Refresh",AutoSize=true};
    readonly Button _next=new(){Text="Next page",AutoSize=true,Enabled=false};
    readonly DataGridView _rows=new(){Dock=DockStyle.Fill,ReadOnly=true,AutoGenerateColumns=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,AccessibleName="Risk invocation history"};
    readonly TextBox _details=new(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,AccessibleName="Risk decision details"};
    readonly TextBox _evidence=new(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,AccessibleName="Risk technical evidence"};
    StrategyWorkflowId? _selectedWorkflow;
    Guid _selectedInvocation;
    readonly Label _status=new(){Dock=DockStyle.Bottom,Height=32,ForeColor=Color.White};
    readonly CancellationTokenSource _lifetime=new();
    string? _cursor;
    long _generation;
    public RiskHistoryForm(IRiskQueryApi api,int portfolio,int fund,StrategyWorkflowId? workflow=null,Guid? invocation=null)
    {
        _api=api;_portfolio=portfolio;_fund=fund;
        Text=$"Risk history - Portfolio {portfolio}, Fund {fund}";Width=1300;Height=850;MinimumSize=new(850,550);
        var toolbar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=45,BackColor=Color.Black};
        toolbar.Controls.AddRange([new Label{Text="Evaluation date (UTC)",AutoSize=true,ForeColor=Color.White},_date,_refresh,_next]);
        var split=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};
        split.RowStyles.Add(new(SizeType.Percent,35));split.RowStyles.Add(new(SizeType.Percent,65));
        var tabs=new TabControl {Dock=DockStyle.Fill};
        var decision=new TabPage("Decision and sizing");decision.Controls.Add(_details);
        var evidence=new TabPage("Evidence");evidence.Controls.Add(_evidence);tabs.TabPages.AddRange([decision,evidence]);
        split.Controls.Add(_rows,0,0);split.Controls.Add(tabs,0,1);
        var frame=new Panel {Dock=DockStyle.Fill,Padding=new Padding(3),BackColor=Color.Gray};
        frame.Controls.Add(split);frame.Controls.Add(toolbar);frame.Controls.Add(_status);Controls.Add(frame);
        _details.BackColor=Color.Black;_details.ForeColor=Color.White;_details.Font=new("Consolas",10);
        _refresh.Click+=async(_,_)=>{await LoadPageAsync(false);if(_selectedWorkflow is {} selected)await OpenAsync(selected,_selectedInvocation);};
        _date.ValueChanged+=(_,_)=>{_cursor=null;_next.Enabled=false;_selectedWorkflow=null;_details.Clear();_evidence.Clear();};
        _next.Click+=async(_,_)=>await LoadPageAsync(true);
        _rows.CellDoubleClick+=async(_,_)=>{if(_rows.CurrentRow?.DataBoundItem is RiskHistoryRow row) await OpenAsync(new(row.WorkflowId),row.InvocationId);};
        _rows.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter && _rows.CurrentRow?.DataBoundItem is RiskHistoryRow row){e.Handled=true;await OpenAsync(new(row.WorkflowId),row.InvocationId);}};
        Shown+=async(_,_)=>{await LoadPageAsync(false);if(workflow is {} id && invocation is {} request)await OpenAsync(id,request);};
        FormClosed+=(_,_)=>{_lifetime.Cancel();_lifetime.Dispose();};
    }
    async Task LoadPageAsync(bool next)
    {
        var generation=++_generation;_refresh.Enabled=_next.Enabled=false;_status.Text="Loading committed Risk history...";
        try
        {
            var response=await _api.GetHistoryAsync(_portfolio,_fund,DateOnly.FromDateTime(_date.Value),25,next?_cursor:null,_lifetime.Token);
            if(IsDisposed || generation!=_generation)return;
            if(!response.Success || response.Value is null)throw new InvalidOperationException(response.ErrorMessage);
            _rows.DataSource=response.Value.Items;_cursor=response.Value.PagingState;
            foreach(var name in new[]{"PortfolioId","FundId","ValueDate","Revision"})if(_rows.Columns.Contains(name))_rows.Columns[name].Visible=false;
            var index=0;foreach(var name in new[]{"EvaluatedAtUtc","Horizon","Variant","Outcome","Units","Reason","OrderId","WorkflowId","InvocationId"})
                if(_rows.Columns.Contains(name))_rows.Columns[name].DisplayIndex=index++;
            if(_rows.Columns.Contains("EvaluatedAtUtc")){_rows.Columns["EvaluatedAtUtc"].HeaderText="Evaluated (UTC)";_rows.Columns["EvaluatedAtUtc"].DefaultCellStyle.Format="yyyy-MM-dd HH:mm:ss";}
            _status.Text=response.Value.Items.Length==0?"No projected Risk history for this UTC date. Projection may still be catching up.":"Double-click an invocation to refresh its details and current authority.";
        }
        catch(OperationCanceledException) when(_lifetime.IsCancellationRequested){}
        catch(Exception e){if(!IsDisposed && generation==_generation)_status.Text=$"History unavailable: {e.Message}";}
        finally{if(!IsDisposed){_refresh.Enabled=true;_next.Enabled=_cursor is not null;}}
    }
    public async Task OpenAsync(StrategyWorkflowId workflow,Guid invocation)
    {
        var generation=++_generation;_details.Clear();_status.Text="Reading exact invocation and current authority...";
        try
        {
            var response=await _api.GetInvocationAsync(workflow,invocation,_lifetime.Token);
            if(IsDisposed || generation!=_generation)return;
            if(!response.Success || response.Value is null)throw new InvalidOperationException(response.ErrorMessage);
            _selectedWorkflow=workflow;_selectedInvocation=invocation;
            _evidence.Text=System.Text.Json.JsonSerializer.Serialize(response.Value,new System.Text.Json.JsonSerializerOptions {WriteIndented=true});
            _details.Text=Render(response.Value);_status.Text="Read-only snapshot; refresh to recheck authority.";
        }
        catch(OperationCanceledException) when(_lifetime.IsCancellationRequested){}
        catch(Exception e){if(!IsDisposed && generation==_generation)_status.Text=$"Risk details unavailable: {e.Message}";}
    }
    public static string Render(RiskObservation observation)
    {
        var view=observation.Snapshot.State;var result=observation.Calculation;var b=new StringBuilder();
        b.AppendLine($"CURRENT AUTHORITY: {observation.CurrentAuthority} (checked {observation.CheckedAtUtc:O})");
        b.AppendLine($"Fund outcome: {observation.FundSynchronization}; projection behind: {observation.ProjectionBehind}");
        b.AppendLine($"Workflow: {view.WorkflowId}; revision: {view.WorkflowRevision}; invocation: {view.RiskExecution?.CommandId}");
        b.AppendLine($"Workflow state: {view.Status}; financial phase: {view.FinancialHandoff?.Phase}; stop reason: {view.StopReasonCode}");
        if(view.RiskExecution is {} input)
        {
            b.AppendLine($"Composer result: {input.CompositionResult.ResultId}; policy: {input.PolicyId} version {input.PolicyVersion}");
            b.AppendLine($"Evaluated: {input.EvaluatedAtUtc:O}; original expiry: {input.ExpiresAtUtc:O}; funding environment: {input.SizingAuthority.Environment}");
        }
        if(result?.Requirements is {} requirement)b.AppendLine($"Settlement cash {requirement.SettlementCash}; margin funding {requirement.MarginFunding}; fees {requirement.FeeReserve}; variation reserve {requirement.VariationReserve}");
        b.AppendLine($"Accepted by workflow: {observation.CalculationAccepted}");
        b.AppendLine($"Historical calculation: {result?.Outcome.ToString() ?? "Not completed"}; units: {result?.StrategyUnits}");
        if(result is not null){b.AppendLine($"Result: {result.ResultId}; hash: {RiskContracts.Hash(result)}");b.AppendLine(string.Join(Environment.NewLine,result.Reasons));}
        if(view.RiskExplanation is {} explanation)
        {
            b.AppendLine($"Explanation schema {explanation.SchemaVersion}; hash {explanation.ContentHash}");
            b.AppendLine($"Available cash {explanation.AvailableCash}; effective loss budget {explanation.EffectiveLossBudget}; market multiplier {explanation.MarketMultiplier}");
            foreach(var condition in explanation.Conditions)b.AppendLine(condition);
            foreach(var q in explanation.Quantities)
            {
                b.AppendLine($"{q.Units} units require {q.Cash} cash; available at evaluation: {explanation.AvailableCash}; fits={q.CashFits}. Loss charge {q.Loss}; budget {explanation.EffectiveLossBudget}; fits={q.LossFits}.");
                foreach(var limit in q.Limits){var bound=explanation.Limits[limit.LimitIndex];b.AppendLine($"  {bound}: proposed={limit.Proposed}; existing={limit.Existing}; fits={limit.Fits}");}
            }
        }
        else b.AppendLine("Detailed explanation unavailable for this historical snapshot.");
        b.AppendLine($"Resize evidence: {view.RiskResize}");
        b.AppendLine("Open Evidence for frozen inputs, source versions, hashes, receipts and Fund state.");
        return b.ToString();
    }
}
