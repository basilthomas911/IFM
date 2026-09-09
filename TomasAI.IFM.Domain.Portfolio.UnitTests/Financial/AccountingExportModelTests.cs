using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Financial;

[Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-07")]
public sealed class AccountingExportModelTests
{
    [Theory]
    [InlineData("missingMapping")] [InlineData("accountVersion")] [InlineData("unbalanced")]
    [InlineData("futureCut")] [InlineData("foreignBook")] [InlineData("duplicateLine")]
    public void Invalid_sources_never_produce_an_export_payload(string change)
    {
        var request=Request();var source=Source();
        if(change=="missingMapping") request=request with { Mapping=request.Mapping with { Accounts=[request.Mapping.Accounts[0]] } };
        if(change=="accountVersion") request=request with { Mapping=request.Mapping with { Accounts=[new(1,2,"cash"),new(2,1,"equity")] } };
        if(change=="unbalanced") source=source with { Journal=source.Journal with { Entries=[source.Journal.Entries[0],source.Journal.Entries[1] with { Credit=99m }] } };
        if(change=="futureCut") source=source with { FinancialRevision=3 };
        if(change=="foreignBook") source=source with { Journal=source.Journal with { BookId=3 } };
        if(change=="duplicateLine") source=source with { Journal=source.Journal with { Entries=[source.Journal.Entries[0],source.Journal.Entries[1] with { Ordinal=1 }] } };
        Action act=()=>AccountingExportModel.Create(request,[source]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Mapping_order_is_not_economic_content_and_corrections_preserve_original_lineage()
    {
        var request=Request();var source=Source() with { ReversesJournalId=9 };
        var one=AccountingExportModel.Create(request,[source]);
        var two=AccountingExportModel.Create(request with { Mapping=request.Mapping with { Accounts=request.Mapping.Accounts.Reverse().ToArray() } },[source]);
        FinancialCanonicalHash.Compute(one).Should().Be(FinancialCanonicalHash.Compute(two));
        one.Journals[0].ReversesJournalId.Should().Be(9);
        one.Journals[0].Lines.Select(x=>(x.Debit,x.Credit)).Should().Equal((100m,0m),(0m,100m));
    }

    static AccountingExportRequest Request()=>new(Guid.Parse("3ac639ed-e3b2-4edf-92b6-554c4e195217"),1,2,2,[10],
        new("internal-fixture",1,[new(1,1,"cash"),new(2,1,"equity")]));
    static AccountingJournalSource Source()=>new(new(10,20,2,3,new DateOnly(2026,9,8),new('A',64),
        [new(1,1,1,3,100m,0,"one"),new(2,2,1,3,0,100m,"two")]),2,null);
}
