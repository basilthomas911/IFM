using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Views.Portfolio;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

[Trait("Category","PortfolioFinancial")]
public sealed class FundFinancialUiTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Financial_views_render_dark_at_minimum_size_and_open_the_selected_journal(bool qualified)
    {
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>
        {
            try
            {
                var api=Substitute.For<IPortfolioFinancialApi>();
                var scope=new FinancialReadScope { PortfolioId=1,FundId=2,Access=new("test",["LedgerRead"],[1]) };
                api.GetAccountBalancesAsync(scope,Arg.Any<GetAccountBalancesRequest>(),Arg.Any<CancellationToken>()).Returns(
                    Ok(new FinancialBalanceSnapshot(1,qualified?"Active":"Importing",
                        [new(101,2,"USD",1000,0,1000),new(102,2,"USD",0,1000,1000)],200,800,qualified)));
                var transaction=new FinancialTransactionRow(401,Guid.NewGuid(),2,0,new() { FundId=2,AccountingDate=new(2026,9,8),
                    TransactionKind=LedgerTransactionKind.DepositConfirmed,Amount=1000,Currency="USD",Description="Confirmed deposit" },501);
                api.GetFundTransactionsPageAsync(scope,Arg.Any<GetFundTransactionsPageRequest>(),Arg.Any<CancellationToken>()).Returns(
                    Ok(new FinancialPage<FinancialTransactionRow>([transaction],null,2)));
                api.GetFundReservationsPageAsync(scope,Arg.Any<GetFundReservationsPageRequest>(),Arg.Any<CancellationToken>()).Returns(
                    Ok(new FinancialPage<FinancialReservationView>([],null,2)));
                api.GetJournalAsync(scope,new(501),Arg.Any<CancellationToken>()).Returns(Ok(new FinancialJournal(501,401,1,2,new(2026,9,8),new('A',64),
                    [new(1,101,1,2,1000,0,"cash"),new(2,102,1,2,0,1000,"capital")])));
                using var form=new FundFinancialForm(api,scope,"Core Futures") { ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-3000,-3000) };
                form.Show(); System.Windows.Forms.Application.DoEvents();
                Field<Label>(form,"_cash").Text.Should().Contain("800");
                if(!qualified) Field<Label>(form,"_status").Text.Should().Contain("unqualified");
                var tabs=Field<TabControl>(form,"_tabs");
                foreach(var size in new[] { new Size(1150,700),form.MinimumSize })
                {
                    form.Size=size; form.PerformLayout(); form.Refresh();
                    for(int index=0;index<tabs.TabPages.Count;index++) tabs.ClientRectangle.Contains(tabs.GetTabRect(index)).Should().BeTrue();
                    foreach(var button in Descendants(form).OfType<Button>().Where(x=>x.Visible))
                        button.Parent!.ClientRectangle.Contains(button.Bounds).Should().BeTrue(button.Text);
                    var grid=Field<DataGridView>(form,"_balances");
                    grid.ReadOnly.Should().BeTrue(); grid.BackgroundColor.Should().Be(Color.Black);
                    grid.DefaultCellStyle.ForeColor.Should().Be(Color.White); grid.Font.SizeInPoints.Should().Be(10);
                    if(Environment.GetEnvironmentVariable("IFM_FINANCIAL_UI_RENDER_DIR") is { Length:>0 } output)
                    {
                        Directory.CreateDirectory(output);
                        using var bitmap=new Bitmap(form.Width,form.Height);
                        form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));
                        bitmap.Save(Path.Combine(output,$"fund-financial-{size.Width}-{qualified}.png"));
                    }
                }
                tabs.SelectedIndex=1;
                var transactions=Field<DataGridView>(form,"_transactions");
                typeof(DataGridView).GetMethod("OnCellDoubleClick",BindingFlags.Instance|BindingFlags.NonPublic)!
                    .Invoke(transactions,[new DataGridViewCellEventArgs(0,0)]);
                System.Windows.Forms.Application.DoEvents();
                tabs.SelectedIndex.Should().Be(3); Field<DataGridView>(form,"_journal").Rows.Count.Should().Be(2);
                form.Close(); done.TrySetResult();
            }
            catch(Exception error) { done.TrySetException(error); }
        }) { IsBackground=true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }
    static ServiceResult<FinancialRead<T>> Ok<T>(T value) where T:class=>new ServiceOk<FinancialRead<T>>(new(FinancialReadStatus.Found,value,2,DateTime.UtcNow));
    static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(value)!;
    static IEnumerable<Control> Descendants(Control parent)=>parent.Controls.Cast<Control>().SelectMany(x=>new[] { x }.Concat(Descendants(x)));
}
