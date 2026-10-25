using System.Reflection;
using System.Text.Json;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Database recovery projection oracle only; never connects to a database or runs a backup.</summary>
internal static class BackupFixtures {
 public static readonly Guid Id=Guid.Parse("d256f7a9-b41c-4fb9-9ec3-eab3497bc86a");
 public static readonly DatabaseBackupOperationReadModel Operation=new(){OperationId=new(Id),ProtectionSetId=new("postgres-main"),Source=BackupSource.LocalWorkstation,Kind=DatabaseRecoveryOperationKind.Backup,Phase=DatabaseRecoveryPhase.Capturing,Outcome=DatabaseRecoveryOutcome.None,ProgressPercent=35,StateRevision=4,CreatedUtc=new(2026,10,26,8,30,0,TimeSpan.FromHours(-4)),SafeDiagnosticReference="fixture/backup",BackupLineage=new(){RequestedMode=DatabaseBackupMode.Automatic,ResolvedMode=DatabaseBackupMode.Incremental,NativeKind=DatabaseNativeBackupKind.PostgreSqlIncremental}};
 public static readonly DatabaseProtectionSetReadModel Protection=new(){ProtectionSetId=new("postgres-main"),Source=BackupSource.LocalWorkstation,Engines=[DatabaseEngine.PostgreSql],Enabled=true,PolicyRevision=7};
 public static readonly DatabaseBackupSetupReadModel Setup=new(){Available=true,BackupHostSettings=new Dictionary<string,string>{{"Enabled","true"},{"PostgreSqlEnabled","true"},{"ScyllaEnabled","false"},{"PostgreSqlProtectionSets","postgres-main, postgres-second"},{"ScyllaProtectionSets","scylla-main"},{"AcceptBackupRequests","true"}}};
 public static readonly DatabaseBackupPolicyReadModel Policy=new(){PolicyId=new("fixture-policy"),EnvironmentIdentity="development",Revision=7,Enforced=true,Definition=new([BackupSource.LocalWorkstation],[new("postgres-main")],new(TimeSpan.FromMinutes(15),TimeSpan.FromMinutes(90)),new(7,4,12),new([DatabaseVerificationLevel.Checksum],TimeSpan.FromHours(24)))};
 public static void Export(string output){
  var map=typeof(DatabaseBackupService).GetMethod("Map",BindingFlags.NonPublic|BindingFlags.Static,null,[typeof(DatabaseBackupOperationReadModel)],null)!;
  FixtureFiles.WriteAllText(Path.Combine(output,"backup-operation-display.json"),JsonSerializer.Serialize(BridgeDocument.Project(map.Invoke(null,[Operation])!,true,true)));
  var d=Policy.Definition;var p=new DatabaseBackupPolicyUiModel(Policy.PolicyId.Value,Policy.Revision,Policy.Enforced,d.EnabledSources,d.ProtectedSets.Select(v=>v.Value).ToArray(),d.RecoveryObjectives.RecoveryPointObjective,d.RecoveryObjectives.RecoveryTimeObjective,d.Retention.DailyCount,d.Retention.WeeklyCount,d.Retention.MonthlyCount,d.Verification.Levels,d.Verification.MaximumVerificationAge);
  FixtureFiles.WriteAllText(Path.Combine(output,"backup-policy-display.json"),JsonSerializer.Serialize(BridgeDocument.Project(p,true,true)));
  var configured=typeof(DatabaseBackupService).GetMethod("ConfiguredProtectionSets",BindingFlags.NonPublic|BindingFlags.Static)!;
  var sets=(List<DatabaseProtectionSetReadModel>)configured.Invoke(null,[BackupSource.LocalWorkstation,new List<DatabaseProtectionSetReadModel>{Protection},Setup.BackupHostSettings])!;
  FixtureFiles.WriteAllText(Path.Combine(output,"backup-configured-display.json"),JsonSerializer.Serialize(BridgeDocument.Project(sets.Select(v=>new DatabaseProtectionSetUiModel(v.ProtectionSetId.Value,v.Source,v.Engines,v.Enabled,v.PolicyRevision)).ToArray(),true,true)));
 }
}
