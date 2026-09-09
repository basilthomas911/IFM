using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;
using TomasAI.IFM.UI.Net.Views.App;

namespace TomasAI.IFM.UI.Net.Views.Portfolio;

/// <summary>Reviews committed-source authority and retains the original mutation until its receipt is known.</summary>
public sealed class PortfolioFinancialAuthorityForm:DarkTradingForm
{
    readonly IPortfolioFinancialApi _api;
    readonly FinancialReadScope _scope;
    readonly FinancialConfigurationOperation _operations;
    readonly CancellationTokenSource _lifetime=new();
    readonly CheckBox _permit=new() { Text="Permit new spending when all authority is ready",AutoSize=true,AccessibleName="Permit qualified new spending" };
    readonly TextBox _reason=PortfolioUiStyle.TextBox("Authority refresh reason");
    readonly TextBox _review=PortfolioUiStyle.TextBox("Prepared financial authority",true);
    readonly Label _status=new() { Dock=DockStyle.Fill,AutoEllipsis=true };
    readonly Button _prepare=PortfolioUiStyle.Button("Prepare","Prepare financial authority");
    readonly Button _save=PortfolioUiStyle.Button("Save","Save financial authority");
    FinancialRead<FinancialAuthorityDraft>? _draft;
    PendingFinancialConfiguration? _pending;
    bool _busy;

    public PortfolioFinancialAuthorityForm(IPortfolioFinancialApi api,FinancialReadScope scope,IPendingFinancialConfigurationStore store)
    {
        _api=api;_scope=scope with { FundId=null };_operations=new(api,store);
        Text="Portfolio financial authority";Name="PortfolioFinancialAuthorityForm";Size=new(900,660);MinimumSize=new(780,610);PortfolioUiStyle.Apply(this);
        var root=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new(14),ColumnCount=2,RowCount=5 };
        root.ColumnStyles.Add(new(SizeType.Absolute,100));root.ColumnStyles.Add(new(SizeType.Percent,100));
        root.RowStyles.Add(new(SizeType.Absolute,38));root.RowStyles.Add(new(SizeType.Absolute,68));root.RowStyles.Add(new(SizeType.Percent,100));
        root.RowStyles.Add(new(SizeType.Absolute,48));root.RowStyles.Add(new(SizeType.Absolute,65));
        root.Controls.Add(_permit,0,0);root.SetColumnSpan(_permit,2);
        root.Controls.Add(new Label { Text="Reason",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft },0,1);
        _reason.Multiline=true;_reason.MaxLength=1024;_reason.Dock=DockStyle.Fill;root.Controls.Add(_reason,1,1);
        _review.Multiline=true;_review.ScrollBars=ScrollBars.Both;_review.WordWrap=false;_review.Dock=DockStyle.Fill;
        root.Controls.Add(_review,0,2);root.SetColumnSpan(_review,2);
        var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,FlowDirection=FlowDirection.RightToLeft };
        var close=PortfolioUiStyle.Button("Close","Close financial authority");close.DialogResult=DialogResult.Cancel;CancelButton=close;
        foreach(var button in new[] { close,_save,_prepare }) { button.MinimumSize=new(125,32);buttons.Controls.Add(button); }
        root.Controls.Add(buttons,0,3);root.SetColumnSpan(buttons,2);root.Controls.Add(_status,0,4);root.SetColumnSpan(_status,2);Controls.Add(root);
        _permit.CheckedChanged+=(_,_)=> { if(_pending is null) { _draft=null;_review.Clear(); }UpdateEnabled(); };
        _prepare.Click+=async(_,_)=>await RunAsync(PrepareAsync);_save.Click+=async(_,_)=>await RunAsync(SaveAsync);
        FormClosed+=(_,_)=>_lifetime.Cancel();_status.Text="Prepare to review current policy, mandate, envelope and deployment limits.";UpdateEnabled();
    }
    async Task PrepareAsync()
    {
        var response=await _api.PrepareFinancialAuthorityAsync(_scope,new(_permit.Checked),_lifetime.Token);
        if(!response.Success || response.Value?.Value is not { } prepared) throw new InvalidOperationException(response.ErrorMessage??"Authority preparation is unavailable.");
        if(IsDisposed) return;_draft=response.Value;
        var lines=new List<string> { $"Financial revision {_draft.FinancialRevision}",string.Join(Environment.NewLine,prepared.Notes),"" };
        foreach(var fund in prepared.Draft.Book!.Funds)
        {
            lines.Add($"Fund {fund.FundId}: {(fund.CanSpend?"New spending permitted":"New spending disabled")}");
            foreach(var deployment in fund.Deployments)
            {
                lines.Add($"Deployment {deployment.Reference.DeploymentKey}: per-trade loss {deployment.MaximumRiskPerTrade:N2} USD; valid until {deployment.Reference.ValidUntilUtc:O}");
                lines.AddRange(deployment.Limits.Select(x=>$"  {x.Measure}: {x.Maximum:N2} {x.Unit}"));
            }
            lines.AddRange(fund.Limits.Select(x=>$"{x.ScopeKind} {x.ScopeKey} / {x.Measure}: {(x.Enabled?x.Maximum.ToString("N2"):"Disabled")} {x.Unit}"));
        }
        _review.Text=string.Join(Environment.NewLine,lines);_status.Text="Review and save. Changed source versions or an expired draft require preparation again.";
    }
    async Task SaveAsync()
    {
        if(_pending is null)
        {
            if(_draft is null) return;
            _pending=new(FinancialControlPreparation.RefreshAuthority(_scope,_draft,_reason.Text,DateTime.UtcNow),PendingFinancialPhase.Prepared,"Prepared.");
        }
        _pending=_pending.Phase==PendingFinancialPhase.Prepared?await _operations.SubmitAsync(_pending.Request,_lifetime.Token)
            :await _operations.RecoverAsync(_pending.Request.OperationId,_lifetime.Token);
        if(!IsDisposed) _status.Text=_pending.Message;
    }
    async Task RunAsync(Func<Task> work)
    {
        if(_busy) return;_busy=true;UpdateEnabled();
        try { await work(); }catch(Exception error) { if(!IsDisposed) _status.Text=error.Message; }
        finally { _busy=false;if(!IsDisposed) UpdateEnabled(); }
    }
    void UpdateEnabled()
    {
        var editing=!_busy && _pending is null;_permit.Enabled=_reason.Enabled=_prepare.Enabled=editing;
        _save.Text=_pending?.Phase switch { PendingFinancialPhase.Committed=>"Saved",PendingFinancialPhase.ExpiredWithoutPosting=>"Expired",null=>"Save",_=>"Check outcome" };
        _save.Enabled=!_busy && (_draft is not null && _pending is null || _pending?.Phase is PendingFinancialPhase.Prepared or PendingFinancialPhase.OutcomeUnknown);
    }
}
