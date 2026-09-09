using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Services.Fund;
using TomasAI.IFM.UI.Net.Views.Portfolio;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

[Trait("Category","PortfolioFinancial")]
public sealed class LegacyFinancialHistoryUiTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Configuration_version_editor_fits_at_minimum_size_and_prepares_a_new_version(bool editRule)
    {
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            try
            {
                var cash=new FinancialConfiguredAccount(new(11,1,"Cash",PostingSide.Debit,true,""),"Active");
                var equity=new FinancialConfiguredAccount(new(12,1,"Equity",PostingSide.Credit,true,""),"Active");
                var rule=new FinancialConfiguredRule(new(Guid.NewGuid(),1,"",LedgerTransactionKind.DepositConfirmed,new(11,1),new(12,1),true),"Active",new(2026,1,1),null);
                var snapshot=new FinancialRead<FinancialLedgerConfiguration>(FinancialReadStatus.Found,new(1,"USD","Emulator","Active","test",
                    [new(Guid.NewGuid(),new(2026,1,1),new(2027,12,31),1,"Open")],[cash,equity],[rule],null),4,DateTime.UtcNow);
                using var form=new PortfolioLedgerVersionForm(new() { PortfolioId=1,Access=new("fixture",["LedgerConfigure"],[1]) },snapshot,
                    editRule?null:cash,editRule?rule:null) { ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-3000,-3000) };
                form.Size=form.MinimumSize;form.Show();System.Windows.Forms.Application.DoEvents();
                var controls=Descendants(form).ToArray();
                foreach(var control in controls.Where(x=>x is Button or ComboBox or TextBox))
                    control.Parent!.ClientRectangle.Contains(control.Bounds).Should().BeTrue(control.AccessibleName);
                controls.OfType<TextBox>().Single(x=>x.AccessibleName=="Configuration version audit reason").Text="Reviewed version change";
                if(Environment.GetEnvironmentVariable("IFM_FINANCIAL_UI_RENDER_DIR") is { Length:>0 } output)
                { Directory.CreateDirectory(output);using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,$"ledger-version-{editRule}.png")); }
                controls.OfType<Button>().Single(x=>x.Text=="Review and save").PerformClick();
                form.PreparedCommand.Should().NotBeNull();form.PreparedCommand!.ExpectedFinancialRevision.Should().Be(4);
                form.PreparedCommand.Body.ExpectedVersion.Should().Be(1);form.PreparedCommand.Body.Reason.Should().Be("Reviewed version change");
                done.TrySetResult();
            }
            catch(Exception error) { done.TrySetException(error); }
        }) { IsBackground=true };
        thread.SetApartmentState(ApartmentState.STA);thread.Start();await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
    [Fact]
    public async Task Retained_history_renders_original_precision_and_has_no_editing_actions()
    {
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            try
            {
                var api=Substitute.For<IFundQueryApi>();
                var row=new FundTransactionReadModel(123,DateTime.Now,FundTransactionType.CashDeposit,44,0,0,default,
                    DateOnly.FromDateTime(DateTime.Today),default,"Original legacy description",0.123456789123456789m,999999m);
                api.GetFundTransactionsAsync(44,Arg.Any<DateOnly>(),Arg.Any<DateOnly>()).Returns(new ServiceOk<FundTransactionReadModel[]>([row]));
                using var form=new Form { Size=new(850,500),ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-3000,-3000) };
                var history=new LegacyFinancialHistoryControl(44,new FundQueryService(api));form.Controls.Add(history);form.Show();
                var controls=Descendants(history).ToArray();controls.OfType<Button>().Single().PerformClick();System.Windows.Forms.Application.DoEvents();
                var grid=controls.OfType<DataGridView>().Single();grid.ReadOnly.Should().BeTrue();grid.AllowUserToAddRows.Should().BeFalse();grid.AllowUserToDeleteRows.Should().BeFalse();
                grid.Rows.Count.Should().Be(1);grid.Rows[0].Cells["OriginalAmount"].Value.Should().Be(row.Amount);
                grid.Rows[0].Cells["LegacyBalance"].Value.Should().Be(row.Balance);grid.Rows[0].Cells["Currency"].Value.Should().Be("Unrecorded");
                grid.Rows[0].Cells["OriginalAmount"].FormattedValue.Should().Be("0.123456789123456789");
                foreach(var button in controls.OfType<Button>()) button.Parent!.ClientRectangle.Contains(button.Bounds).Should().BeTrue();
                if(Environment.GetEnvironmentVariable("IFM_FINANCIAL_UI_RENDER_DIR") is { Length:>0 } output)
                { Directory.CreateDirectory(output);using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,"legacy-financial-history.png")); }
                form.Close();done.TrySetResult();
            }
            catch(Exception error) { done.TrySetException(error); }
        }) { IsBackground=true };
        thread.SetApartmentState(ApartmentState.STA);thread.Start();await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
    static IEnumerable<Control> Descendants(Control parent)=>parent.Controls.Cast<Control>().SelectMany(x=>new[] { x }.Concat(Descendants(x)));
}
