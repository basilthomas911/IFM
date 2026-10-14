using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Rust.Bridge.Net;

// Explicit Development maintenance. Does not run during ordinary UI startup.
if(!args.Contains("--apply",StringComparer.Ordinal)&&!args.Contains("--plan",StringComparer.Ordinal))throw new ArgumentException("Use --apply to restore missing Development reference rows from the published Databento snapshot.");
await using var scope=new RustClientServiceScope(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL")??"nats://localhost:4222");
await scope.StartAsync();
var market=scope.Container.GetInstance<IMarketDataQueryApi>();
var reference=scope.Container.GetInstance<IReferenceQueryApi>();
var referenceCommands=scope.Container.GetInstance<IReferenceCommandApi>();
var marketCommands=scope.Container.GetInstance<IMarketDataCommandApi>();
var now=DateTimeOffset.UtcNow;
List<InstrumentDefinitionSelection> definitions=[];
Guid snapshot=Guid.Empty;
foreach(var root in new[]{"ES","VX"})foreach(var options in new[]{false,true}){
 string? cursor=null;
 do{
  var response=await market.GetInstrumentDefinitionsAsync(new(){Root=root,Options=options,PageSize=200,SnapshotId=snapshot,ContinuationToken=cursor});
  if(!response.Success||response.Value is null)throw new InvalidOperationException(response.ErrorMessage);
  if(snapshot==Guid.Empty)snapshot=response.Value.SnapshotId;
  if(snapshot!=response.Value.SnapshotId)throw new InvalidOperationException("Provider snapshot changed during restoration.");
  definitions.AddRange(response.Value.Items);cursor=response.Value.ContinuationToken;
  if(definitions.Count>100000)throw new InvalidOperationException("Reference restore exceeds bounded market scope.");
 }while(cursor is not null);
}
if(definitions.Count==0)throw new InvalidOperationException("No current ES/VX provider definitions were returned.");
// The raw definition digest is checked before repairing a selection projection.
var config=new Microsoft.Extensions.Configuration.ConfigurationBuilder().SetBasePath(Path.GetFullPath("TomasAI.IFM.Application.Api.Server"))
 .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json").AddEnvironmentVariables().Build();
var connection=config["IFM_TEST_POSTGRES_CONNECTION"]??config["Storage:PostgresConnection"]??config["ConnectionStrings:ReferenceDbConnection"]??throw new InvalidOperationException("Development reference connection required");
if(new Npgsql.NpgsqlConnectionStringBuilder(connection).Database!="ifm_application_development")throw new InvalidOperationException("Restore is restricted to ifm_application_development");
await using var rawConnection=new TomasAI.IFM.Framework.Storage.Postgres.PostgresObjectDataRepositoryConnection().As<Npgsql.NpgsqlConnection>(connection);await rawConnection.OpenAsync();
var settings=TomasAI.IFM.Application.Storage.ApplicationStorageConnections.CreatePostgres(connection);
TomasAI.IFM.Application.Storage.ReferenceDb.ReferenceDbContext? referenceDb=null;
var factory=new TomasAI.IFM.Application.Storage.DbContextFactory(new TomasAI.IFM.Application.Storage.DbContextResolver(_=>referenceDb!));
var log=Microsoft.Extensions.Logging.Abstractions.NullLogger<TomasAI.IFM.Framework.Storage.DbProvider>.Instance;
var sequences=new TomasAI.IFM.Application.Storage.SequenceIdDb.SequenceIdDbContext(settings,factory,log);
using var ids=new TomasAI.IFM.Framework.SequenceId.Postgres.PostgresSequenceIdGenerator(sequences);
referenceDb=new(settings,factory,ids,log);
if(args.Contains("--fresh",StringComparer.Ordinal)){
 if(!args.Contains("--apply",StringComparer.Ordinal))throw new ArgumentException("Fresh persistence requires --apply");
 snapshot=Guid.NewGuid();definitions.Clear();
 using var http=new HttpClient{Timeout=TimeSpan.FromMinutes(5)};
 using var provider=new TomasAI.IFM.Framework.MarketData.DataBento.DatabentoInstrumentDefinitionClient(http);
 var latest=new Dictionary<(ushort,uint),(ulong Received,TomasAI.IFM.Domain.MarketData.Shared.ViewModels.InstrumentDefinitionSelection Selection)>();
 long index=0;List<Task> writes=[];
 await foreach(var row in provider.ReadLatestParentsAsync("GLBX.MDP3",new[]{"ES.FUT","ES.OPT"})){
  var mapped=TomasAI.IFM.Application.MarketData.Contracts.InstrumentDefinitionSelectionMapper.Map(snapshot,index,row);
  writes.Add(referenceDb.InsertInstrumentDefinitionAsync(snapshot,index++,row,CancellationToken.None));
  if(writes.Count==32){await Task.WhenAll(writes);writes.Clear();}
  var key=(row.PublisherId,row.InstrumentId);
  if(!latest.TryGetValue(key,out var previous)||row.ReceivedNanoseconds>=previous.Received)latest[key]=(row.ReceivedNanoseconds,mapped);
 }
 await Task.WhenAll(writes);
 definitions.AddRange(latest.Values.Select(x=>x.Selection).Where(x=>!x.Deleted&&x.ExpirationUtc>now&&x.ActivationUtc<=now&&x.InstrumentClass is "F" or "C" or "P"));
 Console.WriteLine($"Stored {index} exact ES records; {definitions.Count} current definitions. Existing global catalog preserved.");
}
for(int i=0;i<definitions.Count;i++){
 var old=definitions[i];if(old.Root!="ES"||old.Multiplier is not null)continue;
 var pointer=old.RawDefinitionReference.Split(':');
 if(pointer.Length!=5||Guid.Parse(pointer[1])!=snapshot||pointer[2]!=old.Dataset)throw new InvalidDataException("Invalid raw definition reference");
 await using var command=new Npgsql.NpgsqlCommand("SELECT definition_json FROM reference.instrument_definition WHERE snapshot_id=$1 AND dataset=$2 AND bucket=$3 AND record_index=$4",rawConnection);
 command.Parameters.AddWithValue(snapshot);command.Parameters.AddWithValue(old.Dataset);command.Parameters.AddWithValue(int.Parse(pointer[3],CultureInfo.InvariantCulture));command.Parameters.AddWithValue(long.Parse(pointer[4],CultureInfo.InvariantCulture));
 var json=await command.ExecuteScalarAsync() as string??throw new InvalidDataException("Exact provider record is missing");
 var exact=TomasAI.IFM.Framework.MarketData.DataBento.ExactInstrumentDefinition.Parse(old.Dataset,json);
 var mapped=TomasAI.IFM.Application.MarketData.Contracts.InstrumentDefinitionSelectionMapper.Map(snapshot,long.Parse(pointer[4],CultureInfo.InvariantCulture),exact);
 if(mapped.DefinitionDigest!=old.DefinitionDigest||mapped.InstrumentId!=old.InstrumentId)throw new InvalidDataException("Provider facts do not match the selection digest");
 definitions[i]=old with{Multiplier=mapped.Multiplier};
 if(args.Contains("--apply",StringComparer.Ordinal)&&mapped.Multiplier is not null)await referenceDb.InsertInstrumentDefinitionSelectionAsync(definitions[i],CancellationToken.None);
}
if(definitions.Any(x=>x.Root=="ES"&&x.Multiplier is null)){
 var missing=definitions.Where(x=>x.Root=="ES"&&x.Multiplier is null).Take(5).ToArray();
 foreach(var row in missing){var ptr=row.RawDefinitionReference.Split(':');await using var query=new Npgsql.NpgsqlCommand("SELECT definition_json::jsonb - 'hd' FROM reference.instrument_definition WHERE snapshot_id=$1 AND dataset=$2 AND bucket=$3 AND record_index=$4",rawConnection);query.Parameters.AddWithValue(snapshot);query.Parameters.AddWithValue(row.Dataset);query.Parameters.AddWithValue(int.Parse(ptr[3],CultureInfo.InvariantCulture));query.Parameters.AddWithValue(long.Parse(ptr[4],CultureInfo.InvariantCulture));var raw=await query.ExecuteScalarAsync() as string;using var doc=JsonDocument.Parse(raw!);Console.WriteLine(JsonSerializer.Serialize(new{row.InstrumentClass,row.RawSymbol,unit=doc.RootElement.GetProperty("unit_of_measure").ToString(),quantity=doc.RootElement.GetProperty("unit_of_measure_qty").ToString(),display=doc.RootElement.GetProperty("display_factor").ToString(),tick=doc.RootElement.GetProperty("min_price_increment").ToString(),amount=doc.RootElement.GetProperty("min_price_increment_amount").ToString()}));}
 throw new InvalidOperationException("ES provider price-point economics are incomplete or contradictory. No contract records will be created.");
}
var existingReply=await market.GetFuturesContractsAsync();
if(!existingReply.Success||existingReply.Value is null)throw new InvalidOperationException(existingReply.ErrorMessage);
var existing=existingReply.Value.ToDictionary(x=>x.ContractId,StringComparer.Ordinal);
var futures=definitions.Where(x=>x.InstrumentClass=="F").Select(x=>InstrumentDefinitionImport.Future(x,"America/Chicago",now)).ToArray();
var byProvider=futures.ToDictionary(x=>(x.Dataset,x.PublisherId,x.InstrumentId));
var optionsDrafts=definitions.Where(x=>x.InstrumentClass is "C" or "P")
 .Where(x=>byProvider.ContainsKey((x.Dataset,(ushort?)x.PublisherId,(uint?)x.UnderlyingInstrumentId)))
 .Select(x=>InstrumentDefinitionImport.Option(x,byProvider[(x.Dataset,(ushort?)x.PublisherId,(uint?)x.UnderlyingInstrumentId)],"America/Chicago",now)).ToArray();
if(optionsDrafts.GroupBy(x=>x.ContractId).Any(x=>x.Select(y=>y.InstrumentId).Distinct().Count()>1))
 throw new InvalidOperationException("Multiple provider identities map to one domain option ID. Restore requires an explicit mapping decision.");
var lookupFacts=new Dictionary<string,string[]>{
 ["Symbol"]=futures.Select(x=>x.Symbol).Distinct().ToArray(),
 ["SecurityType"]=futures.Select(x=>x.SecurityType).Concat(optionsDrafts.Select(x=>x.SecurityType)).Distinct().ToArray(),
 ["Currency"]=definitions.Select(x=>x.Currency).Distinct().ToArray(),
 ["Exchange"]=definitions.Select(x=>x.Exchange).Distinct().ToArray(),
 ["Multiplier"]=definitions.Where(x=>x.Multiplier is >0).Select(x=>x.Multiplier!.Value.ToString(CultureInfo.InvariantCulture)).Distinct().ToArray(),
 ["OptionType"]=optionsDrafts.Select(x=>x.OptionType).Distinct().ToArray()};
if(args.Contains("--plan",StringComparer.Ordinal)){
 Console.WriteLine(JsonSerializer.Serialize(new{snapshot,definitions=definitions.Count,futures=futures.Length,options=optionsDrafts.Length,lookupFacts,existingContracts=existing.Count,reviewState="Draft"}));return;
}
int lookupAdded=0,futuresAdded=0,optionsAdded=0;
foreach(var (category,codes) in lookupFacts){
 var response=await reference.GetLookupTypesAsync(category);if(!response.Success||response.Value is null)throw new InvalidOperationException(response.ErrorMessage);
 var rows=response.Value.Items;int order=rows.Count==0?0:rows.Max(x=>x.OrderId)+1;
 foreach(var code in codes.Where(x=>!string.IsNullOrWhiteSpace(x)).Order(StringComparer.Ordinal)){
  if(rows.Any(x=>x.ShortCode==code))continue;
  var row=new LookupTypeReadModel(category,code,order++,"Databento "+snapshot,DateTime.UtcNow,"DevelopmentReferenceRestore");
  var command=await referenceCommands.AddLookupTypeAsync(row);if(!command.Success)throw new InvalidOperationException(command.ErrorMessage);
  await Verify(async()=>{var result=await reference.LookupTypeShortCodeExistsAsync(category,code);return result.Success&&result.Value?.Value==true;});lookupAdded++;
 }
}
foreach(var row in futures){
 // Preserve every existing contract, especially the active contract and its reviewed conventions.
 if(existing.ContainsKey(row.ContractId))continue;
 var command=await marketCommands.AddFuturesContractAsync(row,false);if(!command.Success)throw new InvalidOperationException(command.ErrorMessage);
 await Verify(async()=>{var result=await market.GetFuturesContractsAsync();return result.Success&&result.Value?.Any(x=>x.ContractId==row.ContractId&&x.DefinitionDigest==row.DefinitionDigest)==true;});futuresAdded++;
}
foreach(var group in optionsDrafts.GroupBy(x=>x.Symbol)){
 var existingOptions=new HashSet<string>(StringComparer.Ordinal);string? cursor=null;
 do{
  var page=await market.GetFuturesOptionContractsPageAsync(new(group.Key,200,cursor));
  if(!page.Success||page.Value is null)throw new InvalidOperationException(page.ErrorMessage);
  existingOptions.UnionWith(page.Value.Items.Select(x=>x.ContractId));cursor=page.Value.ContinuationToken;
 }while(cursor is not null);
 foreach(var row in group.Where(x=>!existingOptions.Contains(x.ContractId))){
  var command=await marketCommands.AddFuturesOptionContractAsync(row,false);if(!command.Success)throw new InvalidOperationException(command.ErrorMessage);
  // Verify exact provider identity, expiry and decimal strike after projection, not only command acceptance.
  await Verify(async()=>{var result=await market.GetFuturesOptionContractAsync(row.ContractId);return result.Success&&result.Value is {} saved&&saved.InstrumentId==row.InstrumentId&&saved.DefinitionDigest==row.DefinitionDigest&&saved.GetExactStrikePrice()==row.GetExactStrikePrice();});optionsAdded++;
 }
}
Directory.CreateDirectory(".artifacts/rust-ui/tests");
var evidence=new{snapshot,providerDefinitions=definitions.Count,lookupAdded,futuresAdded,optionsAdded,unresolvedUnderlyings=definitions.Count(x=>x.InstrumentClass is "C" or "P")-optionsDrafts.Length,reviewState="Draft",existingContracts="Preserved",completed=DateTimeOffset.UtcNow};
File.WriteAllText(".artifacts/rust-ui/tests/databento-reference-restoration.json",JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(evidence));
static async Task Verify(Func<Task<bool>> predicate){var deadline=DateTime.UtcNow.AddSeconds(30);do{if(await predicate())return;await Task.Delay(100);}while(DateTime.UtcNow<deadline);throw new TimeoutException("Command outcome unknown: expected persisted reference was not observed. Inspect and reload before retrying.");}
