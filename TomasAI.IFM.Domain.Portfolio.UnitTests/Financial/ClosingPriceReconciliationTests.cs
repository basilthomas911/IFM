using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Financial;

public sealed class ClosingPriceReconciliationTests
{
    [Theory]
    [InlineData(true, -1182.50)]
    [InlineData(false, -680)]
    public void Correction_links_original_journals_preserves_fees_and_reclassifies_legacy_double_cash(bool legacy, decimal expectedCashDelta)
    {
        var now=new DateTime(2026,11,18,20,0,0,DateTimeKind.Utc);
        var settlement=Rule(LedgerTransactionKind.TradeSettlement,4,1);
        var realized=Rule(LedgerTransactionKind.RealizedPnl,4,6);
        var oldRealized=legacy?realized with{Version=1,Debit=new(1,1)}:realized;
        var component=Guid.NewGuid();var attempt=Guid.NewGuid();
        var legs=Enumerable.Range(0,4).Select(i=>new TradeLegDefinition{TradeLegId=Guid.NewGuid(),ContractId="LEG"+i,
            CashMultiplier=50,SignedQuantity=i<2?-1:1}).ToArray();
        var fills=legs.Select(l=>new ExecutionFillEvidence{ExecutionFillId=Guid.NewGuid(),TradeLegId=l.TradeLegId,
            ContractId=l.ContractId,ComponentId=component,ExecutionAttemptId=attempt,Price=1,Commission=.65m,
            SignedQuantity=l.SignedQuantity,FilledAtUtc=now.AddMinutes(-1),ExternalExecutionId=l.ContractId}).ToArray();
        var original=new OrderExecutionDefinition{TradeOrderId=new(101,703,4301),ExecutionAttemptId=attempt,
            Status=OrderExecutionStatus.Filled,PositionType=TradeOrderPositionType.Closing,Fills=fills,
            Components=[new(){ComponentId=component,Legs=legs}],Order=new(){Id=new(101,703,4301)}};
        var prices=new[]{2m,80.5m,3.35m,92.75m};
        var corrected=original with{Fills=fills.Select((f,i)=>f with{Price=prices[i],FilledAtUtc=now}).ToArray(),CompletedAtUtc=now};
        var configuration=new FinancialPostingConfiguration(1,703,new(),[settlement,realized],true,DateOnly.FromDateTime(now));
        var posted=fills.Select((f,i)=>new PostedClosingTransaction(2001+i,new(){BookId=1,FundId=703,Currency="USD",
            TransactionKind=LedgerTransactionKind.TradeSettlement,Amount=f.Price*f.SignedQuantity*50,
            Source=new(){SourceEventId=f.ExecutionFillId,OrderId=4201,TradeId=4601}})).ToList();
        posted.Add(new(2009,new(){BookId=1,FundId=703,Currency="USD",Amount=502.5m,TransactionKind=LedgerTransactionKind.RealizedPnl,
            Source=new(){SourceEventId=fills[0].ExecutionFillId,OrderId=4201,TradeId=4601}}));
        var id=Guid.NewGuid();var hash=new string('A',64);
        var batch=ClosingPriceReconciliation.Create(original,corrected,configuration,posted,id,hash,2,now,oldRealized);
        var lines=batch.Items.SelectMany(item=>LedgerPostingModel.Calculate(item,configuration.Rules.Single(r=>r.Kind==item.TransactionKind)).Lines).ToArray();
        Assert.Equal(expectedCashDelta,lines.Where(x=>x.Account.AccountId==1).Sum(x=>x.Debit-x.Credit));
        Assert.Equal(-680m,lines.Where(x=>x.Account.AccountId==6).Sum(x=>x.Credit-x.Debit));
        Assert.DoesNotContain(batch.Items,x=>x.TransactionKind==LedgerTransactionKind.Commission);
        Assert.All(batch.Items,x=>Assert.True(x.RelatedJournalId>=2001));
        Assert.Equal(batch.Items.Length,batch.Items.Select(x=>x.Source.SourceEventId).Distinct().Count());
        Assert.Equal(batch.ManifestHash,ClosingPriceReconciliation.Create(original,corrected,configuration,posted,id,hash,2,now,oldRealized).ManifestHash);
        Assert.All(original.Fills,x=>Assert.Equal(1m,x.Price));
        Assert.Throws<ArgumentException>(()=>ClosingPriceReconciliation.Create(original,corrected with{Fills=corrected.Fills.Select(f=>f with{Commission=0}).ToArray()},configuration,posted,id,hash,2,now,oldRealized));
    }
    static LedgerPostingRule Rule(LedgerTransactionKind kind,int debit,int credit)
    {
        var rule=new LedgerPostingRule(Guid.NewGuid(),2,"",kind,new(debit,1),new(credit,1),true);
        return rule with{ContentHash=FinancialCanonicalHash.Compute(rule)};
    }
}
