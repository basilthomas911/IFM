using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
/// <summary>Committed read fixtures. They contain no callable ledger writer.</summary>
internal static class FinancialLedgerFixtures {
 public static readonly FinancialBalanceSnapshot Balance=new(11,"Active",[new(101,2,"USD",12000.25m,2000m,10000.25m)],125m,9875.25m,true);
 public static readonly FinancialTransactionRow Transaction=new(21,Guid.Parse("d187196d-04b0-4452-8344-bbe014eb4ab2"),7,0,new(){BookId=11,FundId=2,TransactionKind=LedgerTransactionKind.DepositConfirmed,AccountingDate=new(2026,10,26),ValueDate=new(2026,10,26),Currency="USD",Amount=100.25m,Description="Fixture confirmed deposit"},10);
 public static readonly FinancialJournal Journal=new(10,21,11,2,new(2026,10,26),"fixture-journal-hash",[new(0,101,1,2,100.25m,0,"fixture/debit"),new(1,102,1,2,0,100.25m,"fixture/credit")]);
 public static readonly LedgerPostingRule DepositRule=Rule("43da81b8-a8f2-45b1-aa21-5ad8cb080287",LedgerTransactionKind.DepositConfirmed,true);
 public static readonly LedgerPostingRule OpeningRule=Rule("43da81b8-a8f2-45b1-aa21-5ad8cb080288",LedgerTransactionKind.OpeningBalance,false);
 static LedgerPostingRule Rule(string id,LedgerTransactionKind kind,bool confirmed){var rule=new LedgerPostingRule(Guid.Parse(id),3,"",kind,new(101,1),new(102,1),confirmed);return rule with{ContentHash=FinancialCanonicalHash.Compute(rule)};}
 public static FinancialPostingConfiguration Posting(DateOnly day)=>new(11,2,new(){PortfolioVersion=1,FundMandateVersion=1,PolicyId=11,PolicyVersion=1,ValidUntilUtc=StrategyFixtures.At.AddHours(1)},[DepositRule,OpeningRule],true,day,true);
 public static FinancialRead<T> Read<T>(T value) where T:class=>new(FinancialReadStatus.Found,value,7,StrategyFixtures.At);
 public static FinancialPage<FinancialTransactionRow> Transactions(FinancialPageCursor? cursor=null)=>new(cursor is null?[Transaction]:[],cursor is null?new(1,2,7,7,0,"Transactions"):null,7);
 public static FinancialPage<FinancialReservationView> Reservations(FinancialPageCursor? cursor=null)=>new([],cursor is null?new(1,2,7,7,0,"Reservations"):null,7);
}
