using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Api.Server;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
namespace TomasAI.IFM.LivePipeline.IntegrationTests;
public sealed class ParameterSignalStartupTests
{
 [Theory]
 [InlineData(false,false)]
 [InlineData(true,false)]
 [InlineData(false,true)]
 public async Task Preparation_records_every_step_and_isolates_rejection_or_timeout(bool timeout,bool closed)
 {
  var date=new DateOnly(2026,9,10);var run=Guid.NewGuid();
  var sessions=Substitute.For<IFuturesMarketSessionAuthority>();sessions.Current.Returns(new MarketSessionReadModel{OperationalValueDate=date,ActiveValueDate=closed?null:date});
  var contracts=new[]{"ES","VX","VX"}.Select((symbol,i)=>new FuturesContractV3ReadModel(symbol+i,symbol,symbol,symbol+"U6","FUT","USD","CME","50",date.AddDays(i+1),true)).ToArray();
  var authority=Substitute.For<IDatabentoContractAuthority>();
  authority.ReconcileAsync(date,Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(contracts.Select(c=>new FuturesRolloverContractAssignment{ContractRole=DatabentoContractRole.EsQuarterly,RootSymbol=c.Symbol,ContractId=c.ContractId,Description=c.Symbol,LocalSymbol=c.Symbol,SecurityType="FUT",Currency="USD",Exchange="CME",Multiplier="50",LastTradeDate=date.AddDays(10),NextRolloverDate=date.AddDays(9),SourceContractHash="test",CreatedOnUtc=DateTime.UtcNow,CreatedBy="test",UpdatedOnUtc=DateTime.UtcNow,UpdatedBy="test"}).ToArray());
  var catalog=Substitute.For<ICurrentFuturesContractCatalog>();catalog.GetByRootAsync(Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>contracts.Where(x=>x.Symbol==call.Arg<string>()).ToArray());
  var api=Substitute.For<IParameterSetsApi>();ParameterSignalStartupReport? report=null;
  api.RecordStartupReportAsync(Arg.Any<RecordSignalStartupReportCommand>(),Arg.Any<CancellationToken>()).Returns(call=>{report=call.Arg<RecordSignalStartupReportCommand>().Report;return new ServiceOk<GuidResult>(new(Guid.NewGuid()));});
  var analytics=Substitute.For<IMarketDataAnalyticsCommandApi>();
  var pending=new TaskCompletionSource<ServiceResult<Guid>>(TaskCreationOptions.RunContinuationsAsynchronously);
  analytics.StartFuturesRsiSignalAsync(Arg.Any<FuturesRsiSignalEntityId>()).Returns(timeout?pending.Task:Task.FromResult<ServiceResult<Guid>>(new ServiceFailed<Guid>(1,"RSI rejected")));
  analytics.StartFuturesAtrSignalAsync(Arg.Any<FuturesAtrSignalEntityId>()).Returns(new ServiceOk<Guid>(Guid.NewGuid()));
  var runtime=Substitute.For<IParameterRuntimeSnapshot>();runtime.Enabled.Returns(true);
  runtime.Plan.Returns(new ParameterSignalStartupPlan(run,"assignment","plan",[
   new(new(ParameterSignalProducer.Rsi,TimeFrameType.OneMinute,14),true,true,[]),
   new(new(ParameterSignalProducer.Atr,TimeFrameType.OneMinute,14),true,true,[]),
   new(new(ParameterSignalProducer.Adx,TimeFrameType.OneMinute,14),false,true,[])]));
  var activities=new ApiApplicationStartupActivities(sessions,authority,catalog,null!,null!,null!,analytics,null!,null!,null!,null!,null!,null!,new(),
   new(){ParticipantTimeout=TimeSpan.FromMilliseconds(timeout?30:1000)},TimeProvider.System,NullLogger<ApiApplicationStartupActivities>.Instance,api,runtime);
  var context=new ApplicationStartupContext(date,Guid.NewGuid(),Guid.NewGuid(),run);
  await activities.ReconcileCurrentContractsAsync(context,default);
  var result=await activities.PrepareParameterSignalsAsync(context,default);
  Assert.Equal(closed?ApplicationStartupActivityOutcome.ScheduledStopped:ApplicationStartupActivityOutcome.Degraded,result);
  Assert.NotNull(report);Assert.Equal(3,report.Outcomes.Length);
  Assert.Equal(closed?ParameterSignalPreparationStatus.MarketClosed:timeout?ParameterSignalPreparationStatus.TimedOut:ParameterSignalPreparationStatus.Failed,report.Outcomes[0].Status);
  Assert.Equal(closed?ParameterSignalPreparationStatus.MarketClosed:timeout?ParameterSignalPreparationStatus.TimedOut:ParameterSignalPreparationStatus.Accepted,report.Outcomes[1].Status);
  Assert.Equal(ParameterSignalPreparationStatus.NotRequested,report.Outcomes[2].Status);
  if(closed||timeout)await analytics.DidNotReceive().StartFuturesAtrSignalAsync(Arg.Any<FuturesAtrSignalEntityId>());
  pending.TrySetResult(new ServiceOk<Guid>(Guid.NewGuid()));
 }
}
