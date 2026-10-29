using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Host;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared.Recovery;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.Storage;

/// <summary>Composes the actual Host recovery coordinator, native epoch and
/// PostgreSQL intent authority. Unused reference dependencies are strict fixture
/// boundaries: an idle owner must not request reference or provider data.</summary>
static class HostRecoveryCases
{
    internal static async Task VerifyAsync(NativeMarketDataEpochFactory factory,NativeMarketDataEpoch epoch,TimeProvider clock,TimeSpan budget)
    {
        await using var database=await TemporaryPostgres.CreateAsync(TradeSchemaSql.LiveFeedRecoveryCreate);
        var settings=new DbConnectionSettings().Add(TradeDbContext.TradeDbConnection,database.ConnectionString,"System.Data.Postgres");
        var store=new TradeFeedRecoveryStore(settings);
        var admissions=new DatasetWorkerAdmissionRegistry();var health=new OptionFeedProcessingHealth(clock);
        var state=new NativeHostState(factory,admissions,clock,NullLogger<NativeHostState>.Instance,health);
        state.PrepareDate(epoch.ValueDate);
        var market=new NativeCompositionMarketDataApi(factory);
        await using var discovery=new QualifiedCompositionDiscovery(new EuropeanOptionUniverse(Substitute.For<IOptionPricingConventionStore>()),Substitute.For<IOptionPricingContextProvider>(),market,clock);
        var at=clock.GetUtcNow();
        var publication=new TreasuryPublicationPolicy("fixture",at.AddDays(-1),at.AddDays(1),[new(epoch.ValueDate,at.AddHours(-1))]);
        var conversion=new TreasuryRateConversionPolicy("fixture","fixture",TreasuryRateConvention.UsTreasuryCmtNominalSemiannual,"fixture","fixture");
        await using var feeds=new QualifiedIndividualOptionFeeds(discovery,admissions,Substitute.For<IDbContextFactory>(),publication,conversion,timeProvider:clock);
        // Register only instances owned above; the fixture disposes them explicitly.
        var registrations=new ServiceCollection().AddSingleton<ITradeFeedRecoveryStore>(store).AddSingleton(discovery).AddSingleton(feeds);
        await using var services=registrations.BuildServiceProvider();
        var application=new NativeHostApplicationServices(services,factory,clock);
        var recovery=new NativeDatasetRecovery(factory,state,clock,application);
        MarketDataRecoveryRequest Request(TimeSpan remaining)
        {
            var current=state.Capture(true).Datasets.Single(x=>x.Role=="Options");
            return new(Guid.NewGuid(),current.Role,current.Dataset,current.WorkerInstanceId,current.GenerationId,epoch.ValueDate,"qualification",DeadlineUtc:clock.GetUtcNow()+remaining);
        }
        var before=state.Capture(true);var request=Request(budget);
        state.ReportProcessingFailure("qualification",new IOException("transient processing fault"));
        var repaired=await recovery.RecoverAsync(request);var after=state.Capture(true);
        var old=before.Datasets.Single(x=>x.Role=="Options");var next=after.Datasets.Single(x=>x.Role=="Options");
        TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery.OptionRecoveryReplacementProof.Validate(before,old,after,next,request,repaired);
        var read = new MarketDataReadSnapshot(after with{EnabledOptionTrades=[]},[],[],[],NativeOptionWorkload:epoch.CaptureOptionWorkload());
        if(!TomasAI.IFM.Application.Api.Server.Core.Actors.Recovery.OptionRecoveryWorkloadProof.IsComplete(read,next,at,clock.GetUtcNow()))
            throw new InvalidDataException("Actual idle native scope ownership could not prove API workload recovery.");
        var duplicate=await recovery.RecoverAsync(request with{ExpectedWorkerInstanceId=next.WorkerInstanceId,ExpectedGenerationId=next.GenerationId});
        if(duplicate!=repaired||epoch.OptionGeneration!=next.GenerationId||state.Capture(true).Datasets.Single(x=>x.Role=="Options").WorkerInstanceId!=next.WorkerInstanceId)
            throw new InvalidDataException("A repeated successful incident reset the replacement owner again.");
        state.ReportProcessingFailure("new qualification failure",new IOException("new failure after replacement"));
        var continued=await recovery.RecoverAsync(request with{ExpectedWorkerInstanceId=next.WorkerInstanceId,ExpectedGenerationId=next.GenerationId});
        if(!continued.Recovered||continued.GenerationId==next.GenerationId||state.Capture(true).OptionProcessingFailure is not null)
            throw new InvalidDataException("A cached success concealed a later real failure inside the original incident deadline.");
        if(!repaired.Recovered||!repaired.CleanupConfirmed||!repaired.LiveFeedEnabled||repaired.GenerationId!=next.GenerationId
            ||repaired.WorkerInstanceId!=next.WorkerInstanceId||old.GenerationId==next.GenerationId||old.WorkerInstanceId==next.WorkerInstanceId
            ||old.ProcessId!=next.ProcessId||after.OptionProcessingFailure is not null||!next.Healthy)
            throw new InvalidDataException("Concrete Host/native/store recovery lost ownership proof or failed to clear processing evidence.");
        var oldFutures=before.Datasets.Single(x=>x.Role=="Futures");var newFutures=after.Datasets.Single(x=>x.Role=="Futures");
        if(oldFutures.GenerationId!=newFutures.GenerationId||oldFutures.WorkerInstanceId!=newFutures.WorkerInstanceId)
            throw new InvalidDataException("Option recovery replaced the healthy futures owner.");
        var retired=await market.ReleaseAsync("GLBX.MDP3",new("retired-discovery",Guid.NewGuid(),old.GenerationId),default);
        if(retired.Active||retired.Failure is not null)throw new InvalidDataException("An old discovery release blocked application generation transition.");
        // Fail the real storage dependency after replacement. Retries must keep
        // the successful replacement and must terminate under the original budget.
        await Sql("DROP TABLE trade.live_feed_recovery");
        request=Request(TimeSpan.FromMilliseconds(700));var started=Stopwatch.GetTimestamp();
        var failed=await recovery.RecoverAsync(request);var elapsed=Stopwatch.GetElapsedTime(started);
        if(failed.Recovered||failed.CleanupConfirmed||elapsed<TimeSpan.FromMilliseconds(600)||elapsed>TimeSpan.FromSeconds(3)
            ||state.Capture(true).Datasets.Single(x=>x.Role=="Options").Lifecycle!="StoppedError")
            throw new InvalidDataException("Storage failure ended early, renewed the deadline, hung or falsely confirmed cleanup.");
        started=Stopwatch.GetTimestamp();
        if(await recovery.RecoverAsync(request)!=failed||Stopwatch.GetElapsedTime(started)>TimeSpan.FromMilliseconds(250))
            throw new InvalidDataException("A repeated failed incident received another recovery budget.");
        await Sql(TradeSchemaSql.LiveFeedRecoveryCreate);
        var retry=await recovery.RecoverAsync(Request(budget));
        if(!retry.Recovered||!retry.CleanupConfirmed||!state.Capture(true).Datasets.Single(x=>x.Role=="Options").Healthy)
            throw new InvalidDataException("Terminal storage failure left a permanent rejection gate after explicit retry.");
        Console.WriteLine("PASS concrete Host/native/PostgreSQL recovery: same-PID changed owner/generation; idle workload; cleared error; futures untouched; real storage outage retries to original deadline; no false cleanup; explicit recovery after repaired storage.");
        async Task Sql(string text)
        {
            await using var connection=new NpgsqlConnection(database.ConnectionString+";Username=postgres;Password=native-fixture-only");
            await connection.OpenAsync();await using var command=new NpgsqlCommand(text,connection);await command.ExecuteNonQueryAsync();
        }
    }
}
