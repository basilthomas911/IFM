using NSubstitute;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using TomasAI.IFM.UI.Rust.Bridge.Net;

public sealed class DatabaseBackupSessionTests
{
    [Fact]
    public async Task Backup_policy_restore_cancel_and_output_use_the_original_service_and_revision()
    {
        var service = Service(); using var session = new DatabaseBackupSession(service);
        var fields = Fields(); await session.ExecuteAsync(Intent("load", fields));
        fields["protectionSets"]="development";fields["mode"]="Incremental";
        await session.ExecuteAsync(Intent("backup-run", fields));
        await service.Received(1).RequestBackupAsync(BackupSource.LocalWorkstation,"development",7,DatabaseBackupMode.Incremental,Arg.Any<CancellationToken>());
        await session.ExecuteAsync(Intent("backup-policy-load",fields));
        foreach(var (key,value) in new[]{("daily","6"),("weekly","4"),("monthly","2"),("rpoMinutes","60"),("rtoMinutes","120"),("maximumVerificationHours","48")})fields[key]=value;
        await session.ExecuteAsync(Intent("backup-policy-save",fields));
        await service.Received(1).SavePolicyAsync(Arg.Is<DatabaseBackupPolicyUiModel>(p=>p.Revision==7&&p.Daily==6&&p.EnabledSources.Length==1&&p.ProtectionSets[0]=="development"&&p.Rpo==TimeSpan.FromHours(1)),Arg.Any<CancellationToken>());
        fields["protectionSet"]="development";fields["restorePoint"]="verified-point";fields["targetProfile"]="development-on-demand";fields["validationProfile"]="restore-copy";
        await session.ExecuteAsync(Intent("backup-restore",fields));
        await service.Received(1).RequestRestoreDrillAsync(BackupSource.LocalWorkstation,"development","verified-point","development-on-demand","restore-copy",7,Arg.Any<CancellationToken>());
        fields["operationId"]=Operation.OperationId.ToString("D");
        var result=await session.ExecuteAsync(Intent("backup-output",fields));Assert.Contains("retained stdout",result.Context!["output"]);
        await session.ExecuteAsync(Intent("backup-more-output",fields));
        await service.Received(1).LoadLogAsync(BackupSource.LocalWorkstation,Operation.OperationId,15,3,Arg.Any<CancellationToken>());
        var cleared=await session.ExecuteAsync(Intent("backup-clear-output",fields));Assert.Equal("",cleared.Context!["output"]);
        await session.ExecuteAsync(Intent("backup-cancel",fields));
        await service.Received(1).CancelBackupAsync(Operation,Arg.Any<CancellationToken>());
        fields["source"]="AwsCloud";await session.ExecuteAsync(Intent("backup-source",fields));
        await service.Received().LoadSetupAsync(BackupSource.AwsCloud,Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task Read_actions_do_not_submit_backups_and_invalid_or_unloaded_policies_are_rejected()
    {
        var service=Service();using var session=new DatabaseBackupSession(service);var fields=Fields();
        await session.ExecuteAsync(Intent("load",fields));await session.ExecuteAsync(Intent("backup-refresh",fields));
        Assert.DoesNotContain(service.ReceivedCalls(),c=>c.GetMethodInfo().Name is "RequestBackupAsync" or "RequestRestoreDrillAsync" or "SavePolicyAsync");
        await Assert.ThrowsAsync<InvalidOperationException>(()=>session.ExecuteAsync(Intent("backup-policy-save",fields)));
        Assert.False(DatabaseBackupSession.Validate(Intent("execute-shell",fields)));
    }
    [Fact]
    public async Task Protection_selection_and_history_paging_keep_the_setup_source_and_reject_changed_policy_identity()
    {
        var service=Service();using var session=new DatabaseBackupSession(service);var fields=Fields();
        await session.ExecuteAsync(Intent("load",fields));fields["protectionSet"]="development";
        await session.ExecuteAsync(Intent("backup-select",fields));
        await service.Received(1).LoadAsync(BackupSource.LocalWorkstation,"development",Arg.Any<CancellationToken>());
        fields["historySource"]="AwsCloud";var result=await session.ExecuteAsync(Intent("backup-older",fields));
        Assert.Equal("LocalWorkstation",result.Context!["source"]);
        await service.Received().LoadHistoryAsync(BackupSource.AwsCloud,"",Arg.Any<CancellationToken>());
        await session.ExecuteAsync(Intent("backup-policy-load",fields));fields["policyId"]="another-policy";
        await Assert.ThrowsAsync<InvalidOperationException>(()=>session.ExecuteAsync(Intent("backup-policy-save",fields)));
        Assert.DoesNotContain(service.ReceivedCalls(),c=>c.GetMethodInfo().Name=="SavePolicyAsync");
    }
    [Fact]
    public async Task Unavailable_host_metadata_does_not_hide_available_dashboard_and_operation_history()
    {
        var service=Service();service.LoadSetupAsync(Arg.Any<BackupSource>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult<DatabaseBackupSetupUiModel>.Failure(-1,"Setup unavailable"));
        service.LoadHealthAsync(Arg.Any<BackupSource>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult<DatabaseBackupHealthUiModel[]>.Failure(-1,"Readiness unavailable"));
        using var session=new DatabaseBackupSession(service);var result=await session.ExecuteAsync(Intent("load",Fields()));
        Assert.Equal("database-backup",result.View);Assert.Contains("Readiness unavailable",result.Context!["status"]);Assert.NotEmpty(result.Documents["dashboard"]);Assert.NotEmpty(result.Documents["history"]);
        await service.Received(1).LoadHistoryAsync(BackupSource.LocalWorkstation,"",Arg.Any<CancellationToken>());
        await service.Received(1).LoadHistoryAsync(BackupSource.AwsCloud,"",Arg.Any<CancellationToken>());
    }
    static readonly DatabaseBackupOperationUiModel Operation=new(Guid.NewGuid(),"development",BackupSource.LocalWorkstation,default,DatabaseRecoveryOutcome.None,25,"fixture",Engine:DatabaseEngine.PostgreSql);
    static Dictionary<string,string> Fields()=>new(){["source"]="LocalWorkstation",["policyId"]="policy"};
    static ContractEditorIntent Intent(string action,Dictionary<string,string> fields)=>new(1,Guid.NewGuid(),"database-backup",action,0,null,new(fields));
    static IDatabaseBackupService Service()
    {
        var service=Substitute.For<IDatabaseBackupService>();service.CreateNotificationSubscription(Arg.Any<Func<DatabaseBackupNotificationUiModel,ValueTask>>()).Returns(Substitute.For<IUiEventSubscription>());
        service.LoadAsync(Arg.Any<BackupSource>(),Arg.Any<string?>(),Arg.Any<CancellationToken>()).Returns(call=>UiOperationResult<DatabaseBackupDashboardUiModel>.Success(new(call.ArgAt<BackupSource>(0),[new("development",call.ArgAt<BackupSource>(0),[DatabaseEngine.PostgreSql],true,7)],[Operation],null,null)));
        service.LoadHistoryAsync(Arg.Any<BackupSource>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(call=>UiOperationResult<DatabaseBackupHistoryUiModel>.Success(new(call.ArgAt<BackupSource>(0)==BackupSource.LocalWorkstation?[Operation]:[],"")));
        service.LoadSetupAsync(Arg.Any<BackupSource>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult<DatabaseBackupSetupUiModel>.Success(new(new Dictionary<string,string>{{"Environment","Development"}},true)));
        service.LoadHealthAsync(Arg.Any<BackupSource>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult<DatabaseBackupHealthUiModel[]>.Success([]));
        service.LoadPolicyAsync(Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult<DatabaseBackupPolicyUiModel>.Success(new("policy",7,true,[BackupSource.LocalWorkstation],["development"],TimeSpan.FromMinutes(30),TimeSpan.FromHours(1),5,4,2,[],TimeSpan.FromHours(24))));
        service.LoadLogAsync(Arg.Any<BackupSource>(),Arg.Any<Guid>(),Arg.Any<long>(),Arg.Any<long>(),Arg.Any<CancellationToken>()).Returns(UiOperationResult<DatabaseBackupLogUiModel>.Success(new([new(3,DateTimeOffset.UtcNow,default,default,25)],"retained stdout",15,true,true)));
        var accepted=UiOperationResult<DatabaseBackupAcceptedUiModel>.Success(new(Guid.NewGuid()));
        service.RequestBackupAsync(Arg.Any<BackupSource>(),Arg.Any<string>(),Arg.Any<long>(),Arg.Any<DatabaseBackupMode>(),Arg.Any<CancellationToken>()).Returns(accepted);
        service.SavePolicyAsync(Arg.Any<DatabaseBackupPolicyUiModel>(),Arg.Any<CancellationToken>()).Returns(accepted);
        service.RequestRestoreDrillAsync(Arg.Any<BackupSource>(),Arg.Any<string>(),Arg.Any<string>(),Arg.Any<string>(),Arg.Any<string>(),Arg.Any<long>(),Arg.Any<CancellationToken>()).Returns(accepted);
        service.CancelBackupAsync(Arg.Any<DatabaseBackupOperationUiModel>(),Arg.Any<CancellationToken>()).Returns(accepted);
        return service;
    }
}
