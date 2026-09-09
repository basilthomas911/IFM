using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;
using TomasAI.IFM.UI.Net.Views.Portfolio;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

[Trait("Category","PortfolioFinancial")]
public sealed class FundFinancialPostingUiTests
{
    [Theory]
    [InlineData(LedgerTransactionKind.DepositConfirmed)]
    [InlineData(LedgerTransactionKind.WithdrawalRequested)]
    [InlineData(LedgerTransactionKind.WithdrawalSettled)]
    [InlineData(LedgerTransactionKind.WithdrawalCancelled)]
    [InlineData(LedgerTransactionKind.Reversal)]
    [InlineData(LedgerTransactionKind.OpeningBalance)]
    public Task Posting_editor_uses_configured_rules_and_committed_receipts(LedgerTransactionKind kind)=>Sta(()=>
    {
        var api=Substitute.For<IPortfolioFinancialApi>();
        var scope=new FinancialReadScope { PortfolioId=1,FundId=2,Access=new("operator",["LedgerRead","LedgerPost","LedgerReverse","LedgerImport"],[1]) };
        var rule=new LedgerPostingRule(Guid.NewGuid(),3,new('A',64),kind,new(101,1),new(102,1),kind is LedgerTransactionKind.DepositConfirmed or LedgerTransactionKind.WithdrawalSettled);
        var date=DateOnly.FromDateTime(DateTime.Today);
        api.GetFinancialPostingConfigurationAsync(scope,Arg.Any<GetFinancialPostingConfigurationRequest>(),Arg.Any<CancellationToken>())
            .Returns(Ok(new FinancialPostingConfiguration(10,2,new(),[rule],true,date,kind==LedgerTransactionKind.OpeningBalance)));
        FinancialTransactionRow? related=kind is LedgerTransactionKind.DepositConfirmed or LedgerTransactionKind.WithdrawalRequested or LedgerTransactionKind.OpeningBalance ? null
            :new(100,Guid.NewGuid(),2,0,new() { FundId=2,TransactionKind=LedgerTransactionKind.WithdrawalRequested },kind==LedgerTransactionKind.Reversal?200:null);
        var original=related is null?null:new LedgerPostingCompletedEvent { OperationId=related.OperationId,
            Receipt=new() { OperationId=related.OperationId,BookId=10,FundId=2,ObligationId=Guid.NewGuid() } };
        LedgerPostingCompletedEvent? completion=null;PostFundTransactionCommand? sent=null;
        api.GetPostingReceiptAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetPostingReceiptRequest>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            var id=call.Arg<GetPostingReceiptRequest>().OperationId;
            var receipt=related?.OperationId==id?original:completion;
            return receipt is null?NotFound<FinancialOperationOutcome>():Ok(new FinancialOperationOutcome { Posting=receipt });
        });
        api.PostAsync(Arg.Any<PostFundTransactionCommand>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            sent=call.Arg<PostFundTransactionCommand>();
            completion=new() { Id=Guid.NewGuid(),OperationId=sent.OperationId,InputHash=sent.InputSha256,
                Receipt=new() { OperationId=sent.OperationId,FundId=2,BookId=10 } };
            return new ValueTask<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(sent.OperationId)));
        });
        var store=new PendingFinancialOperationStore(Path.Combine(AppContext.BaseDirectory,"TestResults","PostingUi",Guid.NewGuid().ToString("N")));
        using(var form=new FundFinancialPostingForm(api,scope,store,related) { ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-3000,-3000) })
        {
            form.Show();Pump(()=>Field<Button>(form,"_submit").Enabled,()=>Field<Label>(form,"_status").Text);
            Field<NumericUpDown>(form,"_amount").Value=125.50m;
            Field<TextBox>(form,"_description").Text="Operator confirmed transaction";
            if(kind!=LedgerTransactionKind.OpeningBalance) { Field<TextBox>(form,"_reference").Text="movement-123";Field<CheckBox>(form,"_confirmed").Checked=true; }
            form.Size=form.MinimumSize;form.PerformLayout();form.Refresh();
            foreach(var control in Descendants(form).Where(x=>x.Visible && x is Button or TextBox or ComboBox or DateTimePicker or NumericUpDown))
                control.Parent!.ClientRectangle.Contains(control.Bounds).Should().BeTrue(control.AccessibleName);
            Field<TextBox>(form,"_description").Multiline.Should().BeTrue();
            Field<TextBox>(form,"_description").BackColor.Should().Be(Color.Black);
            Field<TextBox>(form,"_description").ForeColor.Should().Be(Color.White);
            Render(form,$"posting-before-{kind}");
            Field<Button>(form,"_submit").Visible.Should().BeTrue("the Post button must remain visible at minimum size");
            Field<Button>(form,"_submit").Enabled.Should().BeTrue();
            Field<Button>(form,"_submit").CanSelect.Should().BeTrue();
            var clicked=false;Field<Button>(form,"_submit").Click+=(_,_)=>clicked=true;
            Field<Button>(form,"_submit").PerformClick();
            clicked.Should().BeTrue($"Post click should fire; busy={Field<bool>(form, "_busy")}, selected={Field<ComboBox>(form, "_kind").SelectedIndex}");
            Pump(()=>Field<Label>(form,"_status").Text.StartsWith("Committed."),()=>$"{Field<Label>(form,"_status").Text}; busy={Field<bool>(form,"_busy")}; pending={Field<PendingFinancialOperation?>(form,"_pending")?.Phase}; sent={sent?.OperationId}; kind={Field<ComboBox>(form,"_kind").SelectedIndex}");
            sent.Should().NotBeNull();sent!.Body.TransactionKind.Should().Be(kind);sent.Body.PostingRule.RuleId.Should().Be(rule.RuleId);
            sent.Body.PostingRule.Version.Should().Be(3);sent.Body.Amount.Should().Be(125.50m);
            if(kind==LedgerTransactionKind.OpeningBalance)
            {
                sent.Body.Source.System.Should().Be("DevelopmentOpeningCapital");sent.Body.MovementEvidence.Status.Should().Be(MovementStatus.Pending);
            }
            Field<Button>(form,"_submit").Enabled.Should().BeFalse();
            Render(form,$"posting-{kind}");form.Close();
        }
        var pendingRead=store.LoadAsync(sent!.OperationId);Pump(()=>pendingRead.IsCompleted);
        var pending=pendingRead.GetAwaiter().GetResult();pending!.Phase.Should().Be(PendingFinancialPhase.Committed);
        using var recovered=new FundFinancialPostingForm(api,scope,store,pending:pending) { ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-3000,-3000) };
        recovered.Show();System.Windows.Forms.Application.DoEvents();
        Field<ComboBox>(recovered,"_kind").Text.Should().Contain(kind.ToString());
        Field<Button>(recovered,"_submit").Enabled.Should().BeFalse();recovered.Close();
    });

    [Fact]
    public Task Closed_period_cannot_dispatch_a_posting()=>Sta(()=>
    {
        var api=Substitute.For<IPortfolioFinancialApi>();var scope=new FinancialReadScope { PortfolioId=1,FundId=2,Access=new("reader",["LedgerRead"],[1]) };
        api.GetFinancialPostingConfigurationAsync(scope,Arg.Any<GetFinancialPostingConfigurationRequest>(),Arg.Any<CancellationToken>())
            .Returns(Ok(new FinancialPostingConfiguration(10,2,new(),[],false,DateOnly.FromDateTime(DateTime.Today))));
        using var form=new FundFinancialPostingForm(api,scope,new PendingFinancialOperationStore(Path.Combine(AppContext.BaseDirectory,"TestResults","unused")))
            { ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-3000,-3000) };
        form.Show();Pump(()=>Field<Label>(form,"_status").Text.Contains("closed"));
        Field<Button>(form,"_submit").Enabled.Should().BeFalse();Render(form,"posting-period-closed");form.Close();
    });
    static void Render(Form form,string name)
    {
        if(Environment.GetEnvironmentVariable("IFM_FINANCIAL_UI_RENDER_DIR") is not { Length:>0 } output) return;
        form.PerformLayout();form.Refresh();System.Windows.Forms.Application.DoEvents();
        Directory.CreateDirectory(output);using var bitmap=new Bitmap(form.Width,form.Height);
        form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,name+".png"));
    }
    static async Task Sta(Action run)
    {
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=> { try { run();done.TrySetResult(); } catch(Exception error) { done.TrySetException(error); } }) { IsBackground=true };
        thread.SetApartmentState(ApartmentState.STA);thread.Start();await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
    static void Pump(Func<bool> complete,Func<string>? diagnostic=null)
    {
        var until=DateTime.UtcNow.AddSeconds(10);
        while(!complete()) { if(DateTime.UtcNow>until) throw new TimeoutException("Financial UI operation did not complete: "+diagnostic?.Invoke());System.Windows.Forms.Application.DoEvents();Thread.Sleep(5); }
    }
    static ServiceResult<FinancialRead<T>> Ok<T>(T value) where T:class=>new ServiceOk<FinancialRead<T>>(new(FinancialReadStatus.Found,value,2,DateTime.UtcNow));
    static ServiceResult<FinancialRead<T>> NotFound<T>() where T:class=>new ServiceOk<FinancialRead<T>>(new(FinancialReadStatus.NotFound,null,2,DateTime.UtcNow));
    static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(value)!;
    static IEnumerable<Control> Descendants(Control parent)=>parent.Controls.Cast<Control>().SelectMany(x=>new[] { x }.Concat(Descendants(x)));
}
