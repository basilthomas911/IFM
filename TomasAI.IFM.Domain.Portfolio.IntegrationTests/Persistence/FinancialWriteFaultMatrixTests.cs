using FluentAssertions;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-02")]
public sealed class FinancialWriteFaultMatrixTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    public static IEnumerable<object[]> WritePoints()=>new[] {
        "ledger_transaction","ledger_journal","ledger_entry","ledger_account_balance","ledger_posting_receipt","financial_source_receipt",
        "financial_operation_receipt","financial_authority","event_log","event_stream_id" }
        .Select(x=>new object[] {false,x}).Concat(new[] {"capacity_reservation","capacity_usage","financial_operation_receipt","financial_authority","event_log","event_stream_id"}
        .Select(x=>new object[] {true,x}));

    [Theory,MemberData(nameof(WritePoints))]
    public async Task Failure_at_each_authoritative_write_rolls_back_business_receipt_and_event_then_original_request_can_retry(bool function,string table)
    {
        _=fixture;var book=function?await CapacityReservationIntegrationTests.FundedBook():await CreateBook();
        var post=Request(book,LedgerTransactionKind.DepositConfirmed,100,0);
        var reserve=function?await CapacityReservationIntegrationTests.ReserveRequest(book,700):null;
        var operation=function?reserve!.OperationId:post.OperationId;
        // Only this transaction enables the generated test trigger. No production failpoint is installed.
        var qualified=table.StartsWith("event_",StringComparison.Ordinal)?$"public.{table}":$"portfolio_financial.{table}";
        var name=$"qualification_fault_{Guid.NewGuid():N}";
        await Transactions().ExecuteAsync(async(db,ct)=>{
            await db.ExecuteAsync($"CREATE FUNCTION portfolio_financial.{name}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF current_setting('ifm.qualification_write_failure',true)='enabled' THEN RAISE EXCEPTION 'Injected financial qualification failure' USING ERRCODE='P0001'; END IF; RETURN NEW; END $$;",[],ct);
            await db.ExecuteAsync($"CREATE TRIGGER {name} BEFORE INSERT OR UPDATE ON {qualified} FOR EACH ROW EXECUTE FUNCTION portfolio_financial.{name}();",[],ct);return true;
        });
        try
        {
            var fault=new WriteFaultTransaction();
            Func<Task> attempt=function
                ?async()=>{await new CapacityReservationStore(fault).ReserveAsync(reserve!,CapacityAdmissionModel.ValidateCommitted,r=>new CapacityReservationCompletedEvent {
                    Id=r.CompletedEventId,EntityId=reserve!.EntityId,Subject=reserve.Subject,CommandId=reserve.CommandId,OperationId=reserve.OperationId,
                    PortfolioId=reserve.PortfolioId,InputHash=reserve.InputSha256,CommittedAtUtc=r.GrantedAtUtc,Receipt=r });}
                :async()=>{await new GeneralLedgerStore(fault).PostAsync(post,[new(Random.Shared.NextInt64(100000,long.MaxValue),Random.Shared.NextInt64(100000,long.MaxValue),post.Body)],
                    (item,rule,prior,original,remaining)=>LedgerPostingModel.Calculate(item,rule,prior,original,remaining),info=>post.Complete(info));};
            var failure=await FluentActions.Awaiting(attempt).Should().ThrowAsync<Npgsql.PostgresException>();failure.Which.SqlState.Should().Be("P0001");
            var rows=await Transactions().ExecuteAsync(async(db,ct)=>(
                Receipts:await db.ScalarAsync("SELECT count(*) FROM portfolio_financial.financial_operation_receipt WHERE portfolio_id=$1 AND operation_id=$2;",[book.PortfolioId,operation],ct),
                Events:await db.ScalarAsync("SELECT count(*) FROM event_log WHERE commandid=$1;",[operation],ct)));
            rows.Receipts.Should().Be(0L);rows.Events.Should().Be(0L);
            var balances=await new FinancialQueryStore(Transactions()).ReadAsync(new() { PortfolioId=book.PortfolioId,Access=post.Access },new GetAccountBalancesRequest());
            balances.FinancialRevision.Should().Be(function?1:0);balances.Value!.AvailableCash.Should().Be(function?1000:0);
            if(function) (await new CapacityReservationStore(Transactions()).ReadCurrentAsync(book.PortfolioId,reserve!.Body.ReservationId)).Should().BeNull();
        }
        finally
        {
            await Transactions().ExecuteAsync(async(db,ct)=>{await db.ExecuteAsync($"DROP TRIGGER {name} ON {qualified}; DROP FUNCTION portfolio_financial.{name}();",[],ct);return true;});
        }
        if(function) (await CapacityReservationIntegrationTests.Reserve(reserve!)).Receipt.FinancialRevision.Should().Be(2);
        else (await Post(post)).Receipt.FinancialRevision.Should().Be(1);
    }
    sealed class WriteFaultTransaction:IPostgresEventTransaction
    {
        public Task<T> ExecuteAsync<T>(Func<EnlistedEventTransaction,CancellationToken,Task<T>> operation,CancellationToken token=default)
            =>Transactions().ExecuteAsync(async(db,ct)=>{await db.ExecuteAsync("SET LOCAL ifm.qualification_write_failure='enabled';",[],ct);return await operation(db,ct);},token);
    }
}
