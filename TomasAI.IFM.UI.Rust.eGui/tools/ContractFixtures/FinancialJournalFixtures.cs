using System.Text.Json;
using MessagePack;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;
/// <summary>Interoperability fixtures only. No financial command is submitted.</summary>
internal static class FinancialJournalFixtures {
 public static readonly Guid PostingId=Guid.Parse("a5ff011f-c20e-40b5-bba7-0ab1baf28cf7");
 public static readonly Guid ConfigurationId=Guid.Parse("a5ff011f-c20e-40b5-bba7-0ab1baf28cf8");
 public static void Export(string output){
  var posting=new PostFundTransactionCommand{OperationId=PostingId,CommandId=PostingId,PortfolioId=1,EntityId=new(1),RequestedAtUtc=StrategyFixtures.At,ExpiresAtUtc=StrategyFixtures.At.AddMinutes(2),ExpectedFinancialRevision=42,Body=new(){BookId=11,FundId=2,TransactionKind=LedgerTransactionKind.DepositConfirmed,AccountingDate=new(2026,10,26),ValueDate=new(2026,10,26),Currency="USD",Amount=100.2500m,Description="Native journal compatibility fixture"},Access=new("Fixture",["LedgerPost"],[1])};
  posting=posting with{InputSha256=FinancialCanonicalHash.Request(posting)};
  var prepared=new PendingFinancialOperation(posting,PendingFinancialPhase.Prepared,"Fixture prepared");
  FixtureFiles.WriteAllBytes(Path.Combine(output,"financial-posting-retained.msgpack"),MessagePackSerializer.Serialize(posting,MessagePackBinarySerializer.ContentOptions));
  var committed=prepared with{Phase=PendingFinancialPhase.Committed,Message="Fixture committed",Completion=new(){OperationId=PostingId,CommandId=PostingId,PortfolioId=1,InputHash=posting.InputSha256,CommittedAtUtc=StrategyFixtures.At}};
  FixtureFiles.WriteAllText(Path.Combine(output,"financial-posting-pending.json"),JsonSerializer.Serialize(prepared));
  FixtureFiles.WriteAllText(Path.Combine(output,"financial-posting-committed.json"),JsonSerializer.Serialize(committed));
  var configuration=new ConfigureLedgerCommand{OperationId=ConfigurationId,CommandId=ConfigurationId,PortfolioId=1,EntityId=new(1),RequestedAtUtc=StrategyFixtures.At,ExpiresAtUtc=StrategyFixtures.At.AddMinutes(2),ExpectedFinancialRevision=42,Access=new("Fixture",["LedgerConfigure"],[1])};
  configuration=configuration with{InputSha256=FinancialCanonicalHash.Request(configuration)};
  FixtureFiles.WriteAllBytes(Path.Combine(output,"financial-configuration-retained.msgpack"),MessagePackSerializer.Serialize(configuration,MessagePackBinarySerializer.ContentOptions));
  FixtureFiles.WriteAllText(Path.Combine(output,"financial-configuration-pending.json"),JsonSerializer.Serialize(new PendingFinancialConfiguration(configuration,PendingFinancialPhase.OutcomeUnknown,"Fixture unknown")));
 }
 public static async Task Verify(string native){
  var posting=Path.Combine(native,"pending-postings");var configuration=Path.Combine(native,"pending-configurations");
  if(!File.Exists(Path.Combine(posting,PostingId.ToString("N")+".json"))||!File.Exists(Path.Combine(configuration,ConfigurationId.ToString("N")+".json")))return;
  var first=await new PendingFinancialOperationStore(posting).LoadAsync(PostingId);
  var second=await new PendingFinancialConfigurationStore(configuration).LoadAsync(ConfigurationId);
  if(first?.Phase!=PendingFinancialPhase.Committed||second?.Phase!=PendingFinancialPhase.OutcomeUnknown)throw new InvalidDataException("Native financial journal changed its persisted outcome");
  Console.WriteLine("Verified both native financial journals with the unchanged managed stores.");
  var verified=0;
  foreach(var folder in new[]{"financial-operations","posting-ui","ledger-ui"}){
  var operations=Path.Combine(native,folder);
  if(Directory.Exists(operations))foreach(var path in Directory.EnumerateFiles(operations,"*.json",SearchOption.AllDirectories)){
   using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(path));var request=doc.RootElement.GetProperty("Request");var operation=request.GetProperty("OperationId").GetGuid();
   if(request.GetProperty("Body").TryGetProperty("TransactionKind",out _)){
    if(await new PendingFinancialOperationStore(Path.GetDirectoryName(path)!).LoadAsync(operation) is null)throw new InvalidDataException("Missing retained native posting");
   }else if(await new PendingFinancialConfigurationStore(Path.GetDirectoryName(path)!).LoadAsync(operation) is null)throw new InvalidDataException("Missing retained native configuration");
   verified++;
  }
  }
  if(verified>0)Console.WriteLine($"Verified {verified} native operation journals with unchanged managed stores.");
 }
}
