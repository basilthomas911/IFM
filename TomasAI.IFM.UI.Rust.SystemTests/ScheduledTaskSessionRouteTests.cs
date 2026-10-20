using NSubstitute;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using TomasAI.IFM.UI.Rust.Bridge.Net;
public sealed class ScheduledTaskSessionRouteTests
{
    [Fact]
    public async Task All_original_setup_and_log_actions_reach_the_existing_scheduler_service()
    {
        var service=Substitute.For<IScheduledTaskService>();var subscription=Substitute.For<IUiEventSubscription>();service.CreateNotificationSubscription(Arg.Any<Func<Guid,ValueTask>>()).Returns(subscription);
        var id=Guid.NewGuid();var schedule=new ScheduledTaskScheduleUiModel("futures-market-close","Close","Development","development",false,"0 1 17 ? * MON-FRI","America/New_York",null,null,1800,60,"");
        var definition=new ScheduledTaskDefinitionUiModel(id,7,2,schedule,false,false,"Applied",2,"Verified",null);
        var run=new ScheduledTaskRunUiModel(Guid.NewGuid(),DateTimeOffset.UtcNow,"Uncertain","Interrupted","fixture",null,null,null,null);
        service.LoadAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<Guid?>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult<ScheduledTaskDashboardUiModel>.Success(new([definition],[],[run],"Ready",null)));
        service.LoadRunHistoryAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<Guid>(),Arg.Any<byte[]?>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult<ScheduledTaskRunPageUiModel>.Success(new([run],[1])));
        service.PreviewAsync(Arg.Any<ScheduledTaskScheduleUiModel>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult<ScheduledTaskPreviewUiModel>.Success(new(true,[],[DateTimeOffset.UtcNow.AddDays(1)])));
        service.SaveAsync(Arg.Any<Guid?>(),Arg.Any<long>(),Arg.Any<ScheduledTaskScheduleUiModel>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult.Success());
        service.SetEnabledAsync(Arg.Any<Guid>(),Arg.Any<long>(),Arg.Any<bool>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult.Success());
        service.RemoveAsync(Arg.Any<Guid>(),Arg.Any<long>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult.Success());
        service.RunNowAsync(Arg.Any<ScheduledTaskDefinitionUiModel>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult.Success());
        service.ResolveUncertainAsync(Arg.Any<ScheduledTaskRunUiModel>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult.Success());
        service.LoadOutputAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<Guid>(),Arg.Any<ScheduledTaskRunUiModel>(),Arg.Any<long>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult<ScheduledTaskOutputPageUiModel>.Success(new("stdout",15,false,true)));
        using var session=new ScheduledTaskSession(service);
        var load=await session.ExecuteAsync(new(1,Guid.NewGuid(),"scheduled-tasks","load",0,null,new(){["environment"]="Development",["hostId"]="development"}));
        var fields=new Dictionary<string,string>(load.Rows[0].Fields){["runId"]=run.Id.ToString("D"),["reason"]="Reviewed interrupted fixture"};
        foreach(var action in new[]{"preview","add","change","enable","disable","run","history","next","output","more-output","resolve","remove"})await session.ExecuteAsync(new(1,Guid.NewGuid(),"scheduled-tasks",action,0,id.ToString("D"),new(fields)));
        await service.Received(1).SaveAsync(null,0,Arg.Any<ScheduledTaskScheduleUiModel>(),Arg.Any<CancellationToken>());
        await service.Received(1).SaveAsync(id,7,Arg.Any<ScheduledTaskScheduleUiModel>(),Arg.Any<CancellationToken>());
        await service.Received(1).SetEnabledAsync(id,7,true,Arg.Any<CancellationToken>());
        await service.Received(1).SetEnabledAsync(id,7,false,Arg.Any<CancellationToken>());
        await service.Received(1).RunNowAsync(definition,Arg.Any<CancellationToken>());
        await service.Received(1).LoadOutputAsync("Development","development",id,run,15,Arg.Any<CancellationToken>());
        await service.Received(1).ResolveUncertainAsync(run,"Reviewed interrupted fixture",Arg.Any<CancellationToken>());
        await service.Received(1).RemoveAsync(id,7,Arg.Any<CancellationToken>());
    }
}
