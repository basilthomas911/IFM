using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ServiceApi;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.ViewModels.Strategy;

namespace TomasAI.IFM.UI.Net.Views.Strategy;

public sealed class StrategyObservationForm:DarkTradingForm,IForm<StrategyObservationForm>
{
    readonly IMarketConditionAssessmentQueryApi _assessments;
    readonly IIntrinsicTimeStrategyWorkflowQueryApi _workflows;
    readonly TextBox _profile=new(){Text="ES.Standard",Width=170};
    readonly ComboBox _horizon=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=105};
    readonly TextBox _workflow=new(){Width=300};
    readonly ListBox _history=new(){Dock=DockStyle.Fill};
    readonly TextBox _details=new(){Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill};
    readonly Button _load=new(){Text="Load history",AutoSize=true};
    readonly Button _open=new(){Text="Open workflow",AutoSize=true};
    readonly CancellationTokenSource _lifetime=new();
    int _revision;
    public StrategyObservationForm(IMarketConditionAssessmentQueryApi assessments,IIntrinsicTimeStrategyWorkflowQueryApi workflows)
    {
        _assessments=assessments;_workflows=workflows;
        Text="Strategy observation";Size=new(1180,720);MinimumSize=new(850,500);StartPosition=FormStartPosition.CenterParent;
        Font=new("Microsoft Sans Serif",10);BackColor=Color.Gray;Padding=new(3);DoubleBuffered=true;
        var body=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,BackColor=Color.Black,Padding=new(10)};
        body.RowStyles.Add(new(SizeType.AutoSize));body.RowStyles.Add(new(SizeType.Percent,100));body.RowStyles.Add(new(SizeType.AutoSize));
        var search=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoSize=true,WrapContents=true};
        search.Controls.AddRange([Label("Market profile"),_profile,Label("Timeframe"),_horizon,_load,Label("Workflow ID"),_workflow,_open]);
        _horizon.Items.AddRange([TimeFrameType.Daily,TimeFrameType.Weekly,TimeFrameType.Monthly]);_horizon.SelectedIndex=0;
        var content=new SplitContainer {Size=new(1100,580),Dock=DockStyle.Fill,SplitterDistance=330,BackColor=Color.Gray};
        content.Panel1.Controls.Add(_history);content.Panel2.Controls.Add(_details);
        var close=new Button {Text="Close",AutoSize=true,DialogResult=DialogResult.Cancel};CancelButton=close;
        close.Click+=(_,_)=>Close();body.Controls.Add(search,0,0);body.Controls.Add(content,0,1);body.Controls.Add(close,0,2);Controls.Add(body);
        foreach(var input in new Control[]{_profile,_horizon,_workflow,_history,_details}) {input.BackColor=Color.Black;input.ForeColor=Color.White;}
        foreach(var button in new[]{_load,_open,close})
        {button.ForeColor=Color.White;button.BackColor=Color.FromArgb(45,45,48);button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderColor=Color.Gray;}
        _load.Click+=async(_,_)=>await RunAsync(LoadHistoryAsync);
        _open.Click+=async(_,_)=>await RunAsync(OpenWorkflowAsync);
        _history.SelectedIndexChanged+=async(_,_)=>
        {
            if(_history.SelectedItem is HistoryItem item) {_workflow.Text=item.Completed.WorkflowId.Value.ToString();await RunAsync(OpenWorkflowAsync);}
        };
        FormClosed+=(_,_)=>{_lifetime.Cancel();_lifetime.Dispose();};
    }
    static Label Label(string text)=>new(){Text=text,AutoSize=true,ForeColor=Color.White,Margin=new(3,7,3,3)};
    async Task RunAsync(Func<int,Task> operation)
    {
        var revision=++_revision;_load.Enabled=_open.Enabled=false;
        try {await operation(revision);}
        catch(OperationCanceledException) when(_lifetime.IsCancellationRequested){}
        catch(Exception ex){if(!IsDisposed&&revision==_revision)_details.Text=ex.Message;}
        finally {if(!IsDisposed&&revision==_revision)_load.Enabled=_open.Enabled=true;}
    }
    async Task LoadHistoryAsync(int revision)
    {
        var result=await _assessments.HistoryAsync(_profile.Text.Trim(),"ES",(TimeFrameType)_horizon.SelectedItem!,DateTime.SpecifyKind(DateTime.MaxValue,DateTimeKind.Utc),25,_lifetime.Token);
        if(IsDisposed||revision!=_revision)return;
        if(!result.Success)throw new InvalidOperationException(result.ErrorMessage);
        _history.Items.Clear();foreach(var item in result.Value??[])_history.Items.Add(new HistoryItem(item));
        _details.Text=_history.Items.Count==0?"No assessment history for this profile and timeframe.":"Select a workflow to inspect its assessment.";
    }
    async Task OpenWorkflowAsync(int revision)
    {
        if(!Guid.TryParse(_workflow.Text.Trim(),out var id))throw new ArgumentException("Enter a valid workflow ID.");
        var workflow=await _workflows.GetByIdAsync(new StrategyWorkflowId(id));
        if(!workflow.Success||workflow.Value is null)throw new InvalidOperationException(workflow.ErrorMessage);
        var view=MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>(workflow.Value.StatePayload);
        MarketConditionAssessmentCompletedEvent? projected=null;
        if(view.AssessmentBinding is not null)
        {
            var result=await _assessments.GetAsync(view.WorkflowId,_lifetime.Token);
            if(result.Success)projected=result.Value;
        }
        if(!IsDisposed&&revision==_revision)_details.Text=MarketAssessmentPresenter.Render(view,projected,DateTime.UtcNow);
    }
    sealed record HistoryItem(MarketConditionAssessmentCompletedEvent Completed)
    {
        public override string ToString()
        {var r=MarketConditionAssessmentContracts.ReadResult(Completed.Result);return $"{r.EvaluatedAtUtc:yyyy-MM-dd HH:mm:ss} {r.TargetHorizon} {r.Assessment.Availability} {r.Assessment.ConditionType}";}
    }
}
