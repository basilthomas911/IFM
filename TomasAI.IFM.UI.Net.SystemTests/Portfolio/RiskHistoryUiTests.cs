using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Views.Strategy;
namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;
public sealed class RiskHistoryUiTests
{
    [Theory]
    [InlineData("Not authorized","Pending",RiskAssessmentOutcome.Rejected)]
    [InlineData("Expired","RiskApproved",RiskAssessmentOutcome.Approved)]
    [InlineData("Unavailable","Unavailable",RiskAssessmentOutcome.Approved)]
    public async Task Read_only_history_pages_and_renders_current_authority_separately_from_calculation(string authority,string synchronization,RiskAssessmentOutcome outcome)
    {
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            try
            {
                System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException,true);
                var api=Substitute.For<IRiskQueryApi>();var workflow=new StrategyWorkflowId(Guid.NewGuid());var invocation=Guid.NewGuid();
                var observation=new RiskObservation(new(){State=new(){WorkflowId=workflow,StopReasonCode=outcome==RiskAssessmentOutcome.Rejected?"RM.CAPACITY.NO_FEASIBLE_QUANTITY":""}},new(){ResultId=invocation,Outcome=outcome,StrategyUnits=outcome==RiskAssessmentOutcome.Approved?2:0},authority,synchronization,DateTime.UtcNow,null,false);
                api.GetInvocationAsync(workflow,invocation,Arg.Any<CancellationToken>()).Returns(new ServiceOk<RiskObservation>(observation));
                api.GetHistoryAsync(1,2,Arg.Any<DateOnly>(),25,null,Arg.Any<CancellationToken>()).Returns(new ServiceOk<RiskHistoryPage>(new([new(1,2,DateOnly.FromDateTime(DateTime.Today),DateTime.UtcNow,workflow.Value,invocation,8,outcome.ToString(),outcome==RiskAssessmentOutcome.Approved?2:0,outcome==RiskAssessmentOutcome.Rejected?"No feasible quantity":"Eligible proposal","Daily",123,"LongFuture")],null)));
                using var form=new RiskHistoryForm(api,1,2,workflow,invocation){ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-3000,-3000)};
                form.Size=form.MinimumSize;form.Show();
                System.Windows.Forms.Application.DoEvents();
                var controls=Descendants(form).ToArray();var grid=controls.OfType<DataGridView>().Single();
                grid.ReadOnly.Should().BeTrue();grid.Rows.Count.Should().Be(1);
                var details=controls.OfType<TextBox>().Single(x=>x.AccessibleName=="Risk decision details");details.ReadOnly.Should().BeTrue();
                details.Text.Should().Contain($"CURRENT AUTHORITY: {authority}").And.Contain($"Historical calculation: {outcome}").And.Contain($"Fund outcome: {synchronization}");
                foreach(var button in controls.OfType<Button>())button.Parent!.ClientRectangle.Contains(button.Bounds).Should().BeTrue();
                if(Environment.GetEnvironmentVariable("IFM_FINANCIAL_UI_RENDER_DIR") is {Length:>0} output)
                {Directory.CreateDirectory(output);using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,$"risk-history-{authority.Replace(" ","-")}.png"));}
                form.Close();done.TrySetResult();
            }
            catch(Exception e){done.TrySetException(e);}
        }){IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
    static IEnumerable<Control> Descendants(Control parent)=>parent.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Descendants(x)));
}
