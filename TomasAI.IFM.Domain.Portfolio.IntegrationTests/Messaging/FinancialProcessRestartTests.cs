using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancialProcess"),Trait("Gate","PF-FIN-02")]
public sealed class FinancialProcessRestartTests(PortfolioEventStoreFixture fixture):IClassFixture<PortfolioEventStoreFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Independent_processes_commit_once_and_a_new_process_replays_after_both_exit(bool function)
    {
        _=fixture;using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var broker=Environment.GetEnvironmentVariable("IFM_FINANCIAL_TEST_NATS_URL")??throw new InvalidOperationException("An isolated broker is required.");
        var book=function?await CapacityReservationIntegrationTests.FundedBook():await CreateBook();
        var posting=Request(book,LedgerTransactionKind.DepositConfirmed,100,0);
        var reservation=function?await CapacityReservationIntegrationTests.ReserveRequest(book,700,validity:TimeSpan.FromSeconds(60)):null;
        var subject=function?reservation!.Subject:posting.Subject;
        var producer=new NatsActorProducer(new NatsProducerOptions { Url=broker },NullLogger.Instance);
        await producer.StartAsync(new(ActorType.Command,$"FinancialProcessTest{Guid.NewGuid():N}"),deadline.Token);
        try
        {
            var api=new PortfolioFinancialApi(producer);
            for(var pass=0;pass<2;pass++)
            {
                var workers=Enumerable.Range(0,pass==0?2:1).Select(_=>Start(function?"Reserve":"Post",subject.ToString(),broker)).ToArray();
                var errors=workers.ToDictionary(process=>process.Id,process=>process.StandardError.ReadToEndAsync(deadline.Token));
                try
                {
                    await Task.WhenAll(workers.Select(async process=>{
                        var line=await process.StandardOutput.ReadLineAsync(deadline.Token);
                        line.Should().Be("FINANCIAL_WORKER_READY",line is null?await errors[process.Id]:line);
                    }));
                    if(function)
                    {
                        var response=await api.ReserveAsync(reservation!,deadline.Token);response.Success.Should().BeTrue(response.ErrorMessage);
                        response.Value!.Completed!.Receipt.FinancialRevision.Should().Be(2);
                    }
                    else { var response=await api.PostAsync(posting,deadline.Token);response.Success.Should().BeTrue(response.ErrorMessage); }
                    foreach(var process in workers)
                    {
                        await process.WaitForExitAsync(deadline.Token);process.ExitCode.Should().Be(0,await errors[process.Id]);
                    }
                }
                finally { foreach(var process in workers) { if(!process.HasExited) process.Kill(entireProcessTree:true);process.Dispose(); } }
            }
            var balances=await new FinancialQueryStore(Transactions()).ReadAsync(new() { PortfolioId=book.PortfolioId,Access=posting.Access },new GetAccountBalancesRequest());
            balances.FinancialRevision.Should().Be(function?2:1);balances.Value!.AvailableCash.Should().Be(function?300:100);
        }
        finally { await producer.StopAsync(); }
    }
    static Process Start(string operation,string subject,string broker)
    {
        var info=new ProcessStartInfo("dotnet") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=AppContext.BaseDirectory };
        info.ArgumentList.Add(typeof(FinancialProcessHost).Assembly.Location);info.ArgumentList.Add("--financial-test-worker");info.ArgumentList.Add(operation);info.ArgumentList.Add(subject);
        info.Environment["IFM_FINANCIAL_TEST_NATS_URL"]=broker;
        return Process.Start(info)??throw new InvalidOperationException("Financial test worker did not start.");
    }
}
