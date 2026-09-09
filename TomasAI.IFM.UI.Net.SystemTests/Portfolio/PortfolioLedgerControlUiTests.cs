using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;
using TomasAI.IFM.UI.Net.Views.Portfolio;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

[Trait("Category","PortfolioFinancial")]
public sealed class PortfolioLedgerControlUiTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task Authority_review_preserves_revision_and_requires_reprepare_after_option_change(bool permit)=>Sta(()=>
    {
        var api=Substitute.For<IPortfolioFinancialApi>();var scope=new FinancialReadScope { PortfolioId=17,Access=new("test",["PortfolioAdministrator"]) };
        var draft=new LedgerConfigurationRequest { Action=LedgerConfigurationAction.RefreshAuthority,BookId=301,
            Book=new() { BookId=301,PortfolioId=17,Funds=[new() { FundId=18,CanSpend=permit }] },SourceCut="review:5" };
        api.PrepareFinancialAuthorityAsync(Arg.Any<FinancialReadScope>(),Arg.Any<PrepareFinancialAuthorityRequest>(),Arg.Any<CancellationToken>())
            .Returns(Ok(new FinancialAuthorityDraft(draft,["Labelled authority fixture"])));
        ConfigureLedgerCommand? sent=null;LedgerConfigurationCompletedEvent? completed=null;
        api.GetPostingReceiptAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetPostingReceiptRequest>(),Arg.Any<CancellationToken>())
            .Returns(_=>new ServiceOk<FinancialRead<FinancialOperationOutcome>>(new(completed is null?FinancialReadStatus.NotFound:FinancialReadStatus.Found,
                completed is null?null:new() { Configuration=completed },5,DateTime.UtcNow)));
        api.ConfigureAsync(Arg.Any<ConfigureLedgerCommand>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            sent=call.Arg<ConfigureLedgerCommand>();completed=new() { Id=Guid.NewGuid(),OperationId=sent.OperationId,CommandId=sent.CommandId,PortfolioId=17,
                InputHash=sent.InputSha256,Receipt=new(sent.OperationId,301,sent.Body.Action,6,DateTime.UtcNow,null,"Active") };
            return new ValueTask<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(sent.CommandId)));
        });
        var store=new PendingFinancialConfigurationStore(Path.Combine(AppContext.BaseDirectory,"TestResults","Authority",Guid.NewGuid().ToString("N")));
        using var form=new PortfolioFinancialAuthorityForm(api,scope,store) { ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-3000,-3000) };
        form.Show();Field<Button>(form,"_save").Enabled.Should().BeFalse();Field<CheckBox>(form,"_permit").Checked=permit;
        Field<Button>(form,"_prepare").PerformClick();Pump(()=>Field<Button>(form,"_save").Enabled);
        Field<CheckBox>(form,"_permit").Checked=!permit;Field<Button>(form,"_save").Enabled.Should().BeFalse();
        Field<CheckBox>(form,"_permit").Checked=permit;Field<Button>(form,"_prepare").PerformClick();Pump(()=>Field<Button>(form,"_save").Enabled);
        Field<TextBox>(form,"_reason").Text="Reviewed committed authority";
        form.Size=form.MinimumSize;form.PerformLayout();form.Refresh();
        foreach(var control in Descendants(form).Where(x=>x.Visible && x is Button or TextBox or CheckBox))
            control.Parent!.ClientRectangle.Contains(control.Bounds).Should().BeTrue($"{control.AccessibleName}: {control.Bounds}");
        Field<TextBox>(form,"_review").ReadOnly.Should().BeTrue();
        Field<Button>(form,"_save").PerformClick();Pump(()=>Field<Label>(form,"_status").Text.StartsWith("Committed."));Pump(()=>!Field<bool>(form,"_busy"));
        sent!.ExpectedFinancialRevision.Should().Be(5);sent.Body.Book!.Funds.Single().CanSpend.Should().Be(permit);
        Field<Button>(form,"_save").Enabled.Should().BeFalse();
        if(Environment.GetEnvironmentVariable("IFM_FINANCIAL_UI_RENDER_DIR") is { Length:>0 } directory)
        {
            Directory.CreateDirectory(directory);form.Refresh();System.Windows.Forms.Application.DoEvents();
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(directory,$"ledger-authority-{permit}.png"));
        }
        form.Close();
    });

    [Fact]
    public Task Development_book_setup_reviews_generated_keys_and_commits_the_original_request()=>Sta(()=>
    {
        var api=Substitute.For<IPortfolioFinancialApi>();var scope=new FinancialReadScope { PortfolioId=17,Access=new("test",["PortfolioAdministrator"]) };
        var draft=new LedgerConfigurationRequest { Action=LedgerConfigurationAction.CreateBook,BookId=301,
            Book=new() { BookId=301,PortfolioId=17,Environment="Emulator",AccountingEntityId=Guid.NewGuid(),ExecutionAccountReference="DEV-ACCOUNT",Funds=[new() { FundId=18 }] },
            Accounts=[new(401,1,"Cash",PostingSide.Debit,true,"")],Rules=[],PeriodId=Guid.NewGuid(),PeriodStart=new(2026,1,1),PeriodEnd=new(2026,12,31) };
        api.PrepareFinancialBookAsync(Arg.Any<FinancialReadScope>(),Arg.Any<PrepareFinancialBookRequest>(),Arg.Any<CancellationToken>())
            .Returns(call=>Ok(new FinancialBookSetup(["DEV-ACCOUNT"],["Development Fund"],call.Arg<PrepareFinancialBookRequest>().ExecutionAccountReference is null?null:draft)));
        ConfigureLedgerCommand? sent=null;LedgerConfigurationCompletedEvent? completed=null;
        api.GetPostingReceiptAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetPostingReceiptRequest>(),Arg.Any<CancellationToken>())
            .Returns(_=>new ServiceOk<FinancialRead<FinancialOperationOutcome>>(new(completed is null?FinancialReadStatus.NotFound:FinancialReadStatus.Found,
                completed is null?null:new() { Configuration=completed },completed is null?0:1,DateTime.UtcNow)));
        api.ConfigureAsync(Arg.Any<ConfigureLedgerCommand>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            sent=call.Arg<ConfigureLedgerCommand>();completed=new() { Id=Guid.NewGuid(),OperationId=sent.OperationId,CommandId=sent.CommandId,
                PortfolioId=17,InputHash=sent.InputSha256,Receipt=new(sent.OperationId,301,LedgerConfigurationAction.CreateBook,1,DateTime.UtcNow,null,"Importing") };
            return new ValueTask<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(sent.CommandId)));
        });
        var store=new PendingFinancialConfigurationStore(Path.Combine(AppContext.BaseDirectory,"TestResults","LedgerSetup",Guid.NewGuid().ToString("N")));
        using var form=new PortfolioLedgerSetupForm(api,scope,store) { ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-3000,-3000) };
        form.Show();Pump(()=>Field<Button>(form,"_prepare").Enabled);
        Field<ComboBox>(form,"_account").DropDownStyle.Should().Be(ComboBoxStyle.DropDownList);
        Field<Button>(form,"_save").Enabled.Should().BeFalse();Field<Button>(form,"_prepare").PerformClick();
        Pump(()=>Field<Button>(form,"_save").Enabled);
        Field<TextBox>(form,"_review").ReadOnly.Should().BeTrue();Field<TextBox>(form,"_review").Text.Should().Contain("301").And.Contain("401");
        Field<TextBox>(form,"_reason").Text="Reviewed development configuration";
        form.Size=form.MinimumSize;form.PerformLayout();form.Refresh();
        foreach(var control in Descendants(form).Where(x=>x.Visible && x is Button or TextBox or ComboBox or DateTimePicker))
            control.Parent!.ClientRectangle.Contains(control.Bounds).Should().BeTrue($"{control.AccessibleName}: {control.Bounds}");
        Field<Button>(form,"_save").PerformClick();Pump(()=>Field<Label>(form,"_status").Text.StartsWith("Committed."));Pump(()=>!Field<bool>(form,"_busy"));
        sent!.Body.BookId.Should().Be(301);sent.Body.Accounts.Single().AccountId.Should().Be(401);sent.ExpectedFinancialRevision.Should().Be(0);
        sent.Body.Book!.MigrationQualified.Should().BeFalse();Field<Button>(form,"_save").Enabled.Should().BeFalse();
        if(Environment.GetEnvironmentVariable("IFM_FINANCIAL_UI_RENDER_DIR") is { Length:>0 } directory)
        {
            Directory.CreateDirectory(directory);form.Refresh();System.Windows.Forms.Application.DoEvents();
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(directory,"ledger-setup.png"));
        }
        form.Close();
    });

    [Theory]
    [InlineData(0,LedgerConfigurationAction.Reconcile)]
    [InlineData(2,LedgerConfigurationAction.ClosePeriod)]
    [InlineData(3,LedgerConfigurationAction.ReopenPeriod)]
    [InlineData(6,LedgerConfigurationAction.QualifyDevelopmentBook)]
    public Task Configured_period_and_reconciliation_actions_commit_and_fit_at_minimum_size(int index,LedgerConfigurationAction expected)=>Sta(()=>
    {
        var api=Substitute.For<IPortfolioFinancialApi>();var scope=new FinancialReadScope { PortfolioId=1,FundId=2,Access=new("test",["PortfolioAdministrator"]) };
        var period=Guid.NewGuid();
        var configuration=new FinancialLedgerConfiguration(10,"USD","Emulator","Active","source:1",
            [new(period,new(2026,1,1),new(2026,12,31),3,expected==LedgerConfigurationAction.ReopenPeriod?"Closed":"Open")],[],[],
            new(Guid.NewGuid(),4,1,2,100,100,[],"ledger-fixture:4",new('A',64)),
            expected==LedgerConfigurationAction.QualifyDevelopmentBook?new() { BookId=10,PortfolioId=1,Environment="Emulator",Funds=[new() { FundId=2 }] }:null);
        api.GetFinancialLedgerConfigurationAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetFinancialLedgerConfigurationRequest>(),Arg.Any<CancellationToken>())
            .Returns(Ok(configuration));
        ConfigureLedgerCommand? sent=null;LedgerConfigurationCompletedEvent? completed=null;
        api.GetPostingReceiptAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetPostingReceiptRequest>(),Arg.Any<CancellationToken>())
            .Returns(_=>new ServiceOk<FinancialRead<FinancialOperationOutcome>>(new(completed is null?FinancialReadStatus.NotFound:FinancialReadStatus.Found,
                completed is null?null:new() { Configuration=completed },5,DateTime.UtcNow)));
        api.ConfigureAsync(Arg.Any<ConfigureLedgerCommand>(),Arg.Any<CancellationToken>()).Returns(call=>
        {
            sent=call.Arg<ConfigureLedgerCommand>();completed=new() { Id=Guid.NewGuid(),OperationId=sent.OperationId,CommandId=sent.CommandId,
                PortfolioId=1,InputHash=sent.InputSha256,Receipt=new(sent.OperationId,10,sent.Body.Action,6,DateTime.UtcNow,
                    expected==LedgerConfigurationAction.Reconcile?sent.OperationId:null,"Active") };
            return new ValueTask<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(sent.CommandId)));
        });
        var store=new PendingFinancialConfigurationStore(Path.Combine(AppContext.BaseDirectory,"TestResults","LedgerControls",Guid.NewGuid().ToString("N")));
        using var form=new PortfolioLedgerControlForm(api,scope,store) { ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-3000,-3000) };
        form.Show();Pump(()=>Field<Button>(form,"_apply").Enabled,()=>Field<Label>(form,"_status").Text);
        Field<ComboBox>(form,"_action").SelectedIndex=index;Field<TextBox>(form,"_reason").Text="Operator confirmed ledger control";
        form.Size=form.MinimumSize;form.PerformLayout();form.Refresh();
        foreach(var control in Descendants(form).Where(x=>x.Visible && x is Button or TextBox or ComboBox or DateTimePicker))
            control.Parent!.ClientRectangle.Contains(control.Bounds).Should().BeTrue($"{control.AccessibleName}: bounds={control.Bounds}, parent={control.Parent.ClientRectangle}, minimum={control.MinimumSize}");
        Field<TextBox>(form,"_reason").BackColor.Should().Be(Color.Black);Field<TextBox>(form,"_reason").ForeColor.Should().Be(Color.White);
        Field<Button>(form,"_apply").PerformClick();
        Pump(()=>Field<Label>(form,"_status").Text.StartsWith("Committed."),()=>Field<Label>(form,"_status").Text);
        Pump(()=>!Field<bool>(form,"_busy"));
        Field<Label>(form,"_status").Width.Should().BeGreaterThan(form.ClientSize.Width-60);
        sent!.Body.Action.Should().Be(expected);sent.ExpectedFinancialRevision.Should().Be(5);
        if(expected is LedgerConfigurationAction.ClosePeriod or LedgerConfigurationAction.ReopenPeriod) { sent.Body.PeriodId.Should().Be(period);sent.Body.ExpectedVersion.Should().Be(3); }
        if(expected==LedgerConfigurationAction.QualifyDevelopmentBook) { sent.Body.Book!.MigrationQualified.Should().BeFalse();sent.Body.ReconciliationId.Should().Be(configuration.LatestReconciliation!.ReconciliationId); }
        var read=store.LoadAsync(sent.OperationId);Pump(()=>read.IsCompleted);read.GetAwaiter().GetResult()!.Phase.Should().Be(PendingFinancialPhase.Committed);
        if(Environment.GetEnvironmentVariable("IFM_FINANCIAL_UI_RENDER_DIR") is { Length:>0 } directory)
        {
            Directory.CreateDirectory(directory);form.Refresh();System.Windows.Forms.Application.DoEvents();
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(directory,$"ledger-{expected}.png"));
        }
        form.Close();
    });
    static Task Sta(Action run)
    {
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=> { try { run();completion.TrySetResult(); } catch(Exception error) { completion.TrySetException(error); } }) { IsBackground=true };
        thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
    static void Pump(Func<bool> done,Func<string>? diagnostic=null)
    {
        var deadline=DateTime.UtcNow.AddSeconds(10);
        while(!done()) { if(DateTime.UtcNow>=deadline) throw new TimeoutException(diagnostic?.Invoke());System.Windows.Forms.Application.DoEvents();Thread.Sleep(5); }
    }
    static ServiceResult<FinancialRead<T>> Ok<T>(T value) where T:class=>new ServiceOk<FinancialRead<T>>(new(FinancialReadStatus.Found,value,5,DateTime.UtcNow));
    static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(value)!;
    static IEnumerable<Control> Descendants(Control parent)=>parent.Controls.Cast<Control>().SelectMany(x=>new[] { x }.Concat(Descendants(x)));
}
