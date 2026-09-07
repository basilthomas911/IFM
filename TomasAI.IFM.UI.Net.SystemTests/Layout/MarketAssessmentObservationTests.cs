using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using FluentAssertions;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Views.Strategy;

namespace TomasAI.IFM.UI.Net.SystemTests.Layout;

/// <summary>Uses exact accepted payloads exported by the real MC-R08 NATS/PG/Scylla test, with no synthetic result.</summary>
public sealed class MarketAssessmentObservationTests
{
    [Theory]
    [InlineData(TimeFrameType.Daily)][InlineData(TimeFrameType.Weekly)][InlineData(TimeFrameType.Monthly)]
    [Trait("Gate","MC-R09")]
    public async Task Observation_form_renders_actual_runtime_result_and_closes_its_message_loop(TimeFrameType horizon)
    {
        var evidence=Environment.GetEnvironmentVariable("IFM_MC_EVIDENCE_DIR");
        evidence.Should().NotBeNullOrWhiteSpace("run the MC-R08 runtime qualification with IFM_MC_EVIDENCE_DIR first");
        var view=MessagePackSerializer.Deserialize<IntrinsicTimeStrategyWorkflowView>(await File.ReadAllBytesAsync(Path.Combine(evidence!,horizon+".workflow.msgpack")));
        var completed=MessagePackSerializer.Deserialize<MarketConditionAssessmentCompletedEvent>(await File.ReadAllBytesAsync(Path.Combine(evidence!,horizon+".assessment.msgpack")));
        var queries=Substitute.For<IMarketConditionAssessmentQueryApi>();var workflows=Substitute.For<IIntrinsicTimeStrategyWorkflowQueryApi>();
        queries.GetAsync(view.WorkflowId,Arg.Any<CancellationToken>()).Returns(new ServiceOk<MarketConditionAssessmentCompletedEvent>(completed));
        queries.HistoryAsync(Arg.Any<string>(),"ES",horizon,Arg.Any<DateTime>(),25,Arg.Any<CancellationToken>()).Returns(new ServiceOk<MarketConditionAssessmentCompletedEvent[]>([completed]));
        var row=new IntrinsicTimeStrategyWorkflowReadModel(view.WorkflowId,view.EntityId.Format(),"Qualification",1,"ES",DateOnly.FromDateTime(view.StartedAtUtc),horizon,
            view.TriggerEventId,view.CorrelationId,StrategyWorkflowStatus.Running,StrategyWorkflowOutcome.None,view.CurrentStage,view.WorkflowRevision,1,1,MessagePackSerializer.Serialize(view),"",view.StartedAtUtc,null,view.UpdatedAtUtc);
        workflows.GetByIdAsync(view.WorkflowId,0).Returns(new ServiceOk<IntrinsicTimeStrategyWorkflowReadModel>(row));
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            try
            {
                using var form=new StrategyObservationForm(queries,workflows);
                form.Shown+=async(_,_)=>
                {
                    try
                    {
                        Field<ComboBox>(form,"_horizon").SelectedItem=horizon;
                        Field<TextBox>(form,"_profile").Text=view.AssessmentBinding!.Parameters.MarketProfileId;
                        Field<Button>(form,"_load").PerformClick();
                        await Wait(()=>Field<ListBox>(form,"_history").Items.Count==1);
                        Field<ListBox>(form,"_history").SelectedIndex=0;
                        var details=Field<TextBox>(form,"_details");await Wait(()=>details.Text.Contains("Matches accepted result",StringComparison.Ordinal));
                        details.Text.Should().Contain(horizon.ToString()).And.Contain("Evidence:").And.NotContain("Tradeability");
                        details.Width.Should().BeGreaterThan(400);details.Height.Should().BeGreaterThan(300);
                        form.Refresh();await Task.Delay(100);
                        using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
                        bitmap.Save(Path.Combine(evidence!,horizon+".observation.png"));
                        ((Button)form.CancelButton!).PerformClick();done.TrySetResult();
                    }
                    catch(Exception error){done.TrySetException(error);form.Close();}
                };
                System.Windows.Forms.Application.Run(form);
            }
            catch(Exception error){done.TrySetException(error);}
        }){IsBackground=true,Name="Market assessment observation qualification"};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();await done.Task.WaitAsync(TimeSpan.FromSeconds(20));
        thread.Join(TimeSpan.FromSeconds(5)).Should().BeTrue("Close must end the observation form's message loop");
    }
    static T Field<T>(object instance,string name)=>(T)instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(instance)!;
    static async Task Wait(Func<bool> ready)
    {var until=DateTime.UtcNow.AddSeconds(5);while(!ready()){if(DateTime.UtcNow>=until)throw new TimeoutException("Observation form did not finish loading");await Task.Delay(20);}}
}
