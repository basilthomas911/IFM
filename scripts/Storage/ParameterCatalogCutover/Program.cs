using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Domain.Reference.ParameterSets.Migration;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using TomasAI.IFM.Shared.Storage;

if(args.Length!=1||args[0] is not ("--inspect" or "--apply" or "--verify" or "--verify-api"))
    throw new ArgumentException("Use --inspect, --apply, --verify or --verify-api.");
Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT","Development");
Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT","Development");
if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("POSTGRES_DEV_KEY")))
    Environment.SetEnvironmentVariable("POSTGRES_DEV_KEY",Environment.GetEnvironmentVariable("POSTGRES_DEV_KEY",EnvironmentVariableTarget.User));
var connection=Environment.GetEnvironmentVariable("IFM_PARAMETER_CUTOVER_CONNECTION")??"Host=localhost;Port=5432;Database=ifm_application_development";
var identity=new NpgsqlConnectionStringBuilder(connection);
if(identity.Host is not ("localhost" or "127.0.0.1")||identity.Database!="ifm_application_development")
    throw new InvalidOperationException("This tool only operates on local ifm_application_development.");
var settings=new DbConnectionSettings();
foreach(var name in new[]{"EventSourceActorDbConnection","ConfigurationDbConnection","MarketDataDbConnection"})settings.Add(name,connection,"System.Data.Postgres");
var resolver=new CutoverResolver(settings);
var db=(ConfigurationDbContext)resolver.Factory.ConfigurationDb;
var transactions=new PostgresEventTransaction(settings);
using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(10));var token=timeout.Token;
var before=await ProofAsync(transactions,token);
var old=await transactions.ExecuteOnceAsync(async(tx,ct)=>new LegacyRows(
    await tx.QueryAsync("SELECT body::text FROM reference_configuration.parameter_set_version ORDER BY set_id,version;",[],r=>JsonSerializer.Deserialize<ParameterSetVersion>(r.GetString(0))!,ct),
    await tx.QueryAsync("SELECT body::text FROM reference_configuration.parameter_assignment_revision ORDER BY assignment_id,revision;",[],r=>new ParameterAssignmentChangedEvent{AssignmentJson=r.GetString(0)}.ReadAssignment(),ct),
    await tx.QueryAsync("SELECT body::text FROM reference_configuration.parameter_startup_run ORDER BY run_id;",[],r=>new ParameterStartupChangedEvent{RunJson=r.GetString(0)}.ReadRun()!,ct),
    await tx.QueryAsync("SELECT body::text FROM reference_configuration.parameter_startup_report ORDER BY run_id,revision;",[],r=>JsonSerializer.Deserialize<ParameterSignalStartupReport>(r.GetString(0))!,ct)),token);
foreach(var version in old.Versions)if(version.Parameters is null)ParameterLegacyUpcaster.Verify(version);
Console.WriteLine($"Source verified: {old.Versions.Count} versions, {old.Assignments.Count} assignment revisions, {old.Runs.Count} runs, {old.Reports.Count} reports; {before.Events} source events.");
if(args[0]=="--inspect")return;
var applied=0;
if(args[0]=="--apply")
{
    await ConfigurationSchemaDb.WithoutLookupDefaults(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
    await new MarketDataSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAsync(["strategy_option_chain_parameter_version","strategy_option_chain_parameter_current"]);
    Console.WriteLine("Additive schema prepared; replaying existing source facts.");
    applied=await new ParameterCatalogMigrationService(transactions,resolver.Factory).ExecuteAsync(token);
    Console.WriteLine($"Committed replay: {applied} facts.");
}
foreach(var original in old.Versions)
{
    var expected=original.AsTyped();
    var actual=(await db.GetParameterSetsAsync(original.Reference.ComponentCode,original.Reference.SetId,token)).Single(x=>x.Reference.Version==original.Reference.Version);
    Same(expected,actual,"version "+original.Reference.SetId);
}
var cacheVersions=old.Versions.Where(x=>x.Reference.ComponentCode==ParameterSchemaRegistry.StrategyOptionChainCacheComponent).ToArray();
foreach(var version in cacheVersions)
{
    var expected=version.Values().Get<TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterSet>();
    Same(expected,await resolver.Factory.MarketDataDb.ReadVersionAsync(version.Reference.SetId,version.Reference.Version,token),"cache policy version");
}
var publishedCache=await resolver.Factory.MarketDataDb.ReadPublishedAsync("Development",token);
var expectedPublished=cacheVersions.Where(x=>x.Status==ParameterVersionStatus.Published).ToArray();
if(publishedCache.Length!=expectedPublished.Length)throw new InvalidDataException("Published cache policy count differs.");
foreach(var version in expectedPublished)Same(version.Values().Get<TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterSet>(),
    publishedCache.Single(x=>x.ParameterSetId==version.Reference.SetId&&x.Version==version.Reference.Version),"published cache policy");
var audited=await transactions.ExecuteOnceAsync(async(tx,ct)=>
{
    foreach(var expected in old.Assignments)Same(expected,await db.GetParameterAssignmentProjectionAsync(tx,expected.AssignmentId,expected.Revision,ct),"assignment "+expected.AssignmentId);
    foreach(var expected in old.Runs)Same(expected,await db.GetParameterStartupProjectionAsync(tx,expected.RunId,ct),"startup "+expected.RunId);
    foreach(var expected in old.Reports)Same(expected,await db.GetParameterStartupReportProjectionAsync(tx,expected.RunId,ct),"report "+expected.RunId);
    var sources=await tx.QueryAsync("SELECT n.eventtypename,l.eventversion,l.eventpayload FROM public.event_log l JOIN public.event_name_id n USING(eventnameid) WHERE n.eventtypename LIKE '%Parameter%' ORDER BY l.eventversion;",[],
        r=>new EventStreamReadModel{EventTypeName=r.GetString(0),EventVersion=r.GetInt64(1),EventData=r.GetFieldValue<byte[]>(2)}.ToDomainEvent(),ct);
    var audits=sources.Select(f=>f switch{IParameterSetFact set=>set.ReadAudit(),ParameterAssignmentChangedEvent assignment=>assignment.ReadAudit(),_=>null}).OfType<ParameterAuditEntry>().ToArray();
    foreach(var audit in audits)Same(audit,await db.GetParameterAuditProjectionAsync(tx,audit.OperationId,ct),"audit "+audit.OperationId);
    var states=await tx.QueryAsync("SELECT l.run_id,l.active,n.active FROM reference_configuration.parameter_startup_run l LEFT JOIN reference_configuration.parameter_catalog_startup_state n USING(run_id) WHERE n.active IS DISTINCT FROM l.active;",[],r=>r.GetGuid(0),ct);
    if(states.Count!=0)throw new InvalidDataException("Startup activation states differ.");
    return audits.Length;
},token);
var second=await new ParameterCatalogMigrationService(transactions,resolver.Factory).ExecuteAsync(token);
if(second!=0)throw new InvalidDataException("Second migration replay was not a no-op.");
if(args[0]=="--verify-api")
{
    await using var verificationHost=await CutoverApiHost.StartAsync(connection,token);
    await using var producer=new NatsActorProducer(new NatsProducerOptions{Url=Environment.GetEnvironmentVariable("IFM_PARAMETER_CUTOVER_NATS")??"nats://localhost:4222"},NullLogger.Instance);
    await producer.StartAsync(new ActorMailboxId(ActorType.Query,"ParameterCutoverVerifier"));
    var api=new ParameterSetsApi(producer);
    foreach(var expected in old.Versions)
    {
        var result=await api.VersionsAsync(expected.Reference.SetId,token,expected.Reference.ComponentCode);
        if(!result.Success||result.Value is null)throw new InvalidDataException(result.ErrorMessage??"API versions failed");
        Same(expected.AsTyped(),result.Value.Single(x=>x.Reference.Version==expected.Reference.Version),"API version");
        var state=await api.StateAsync(expected.Reference.SetId,token);
        if(!state.Success||state.Value is null)throw new InvalidDataException(state.ErrorMessage??"API state failed");
        Same(expected.AsTyped(),state.Value.Versions.Single(x=>x.Reference.Version==expected.Reference.Version),"API source state");
    }
    var components=await api.ComponentsAsync(token);
    if(!components.Success||components.Value is null||components.Value.Length!=8)throw new InvalidDataException("Eight parameter components were not exposed by the API.");
    foreach(var component in components.Value)
    {
        var id=Guid.NewGuid();var preview=await api.PreviewValuesAsync(id,component.ComponentCode,token:token);
        if(!preview.Success||preview.Value is null||preview.Value.SetId!=id)throw new InvalidDataException(preview.ErrorMessage??"Typed draft preview failed");
        var validation=await api.ValidateValuesAsync(preview.Value,component.ComponentCode,preview.Value.SchemaVersion,token);
        if(!validation.Success||validation.Value?.IsValid!=true||validation.Value.CandidateSha256!=ParameterValueCodec.Hash(preview.Value))
            throw new InvalidDataException(validation.ErrorMessage??"Typed draft validation failed");
    }
    var runs=await api.StartupRunsAsync(token);
    if(!runs.Success||runs.Value is null)throw new InvalidDataException(runs.ErrorMessage??"API runs failed");
    foreach(var run in runs.Value)
    {
        Same(old.Runs.Single(x=>x.RunId==run.RunId),run,"API frozen startup");
    }
    await producer.StopAsync();Console.WriteLine($"API read verification passed: {old.Versions.Count} versions and {runs.Value.Length} retained startup generations.");
}
var after=await ProofAsync(transactions,token);
if(before!=after)throw new InvalidDataException("Source event bytes or legacy configuration rows changed during cutover verification.");
var evidence=new{Database=identity.Database,CompletedAtUtc=DateTime.UtcNow,Mode=args[0],AppliedSourceFacts=applied,SecondRunFacts=second,
    CachePolicies=cacheVersions.Length,PublishedCachePolicies=publishedCache.Length,Versions=old.Versions.Count,AssignmentRevisions=old.Assignments.Count,StartupRuns=old.Runs.Count,StartupReports=old.Reports.Count,Audits=audited,
    Source=after,SourceUnchanged=true,ValuesAndMetadataVerified=true};
var directory=Environment.GetEnvironmentVariable("IFM_PARAMETER_CUTOVER_EVIDENCE")??".artifacts/parameter-catalog-cutover";
Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,args[0][2..]+"-evidence.json"),JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(JsonSerializer.Serialize(evidence));

static void Same<T>(T expected,T? actual,string scope)
{
    var options=MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.None);
    if(actual is null||!MessagePackSerializer.Serialize(expected,options).AsSpan().SequenceEqual(MessagePackSerializer.Serialize(actual,options)))
        throw new InvalidDataException("Cutover equality mismatch: "+scope);
}
static Task<SourceProof> ProofAsync(PostgresEventTransaction transactions,CancellationToken token)=>transactions.ExecuteOnceAsync(async(tx,ct)=>
{
    var parts=await tx.QueryAsync("SELECT l.eventversion,n.eventtypename,l.eventpayload FROM public.event_log l JOIN public.event_name_id n USING(eventnameid) WHERE n.eventtypename LIKE '%Parameter%' ORDER BY l.eventversion;",[],r=>(Version:r.GetInt64(0),Type:r.GetString(1),Bytes:r.GetFieldValue<byte[]>(2)),ct);
    using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    foreach(var row in parts){hash.AppendData(BitConverter.GetBytes(row.Version));hash.AppendData(Encoding.UTF8.GetBytes(row.Type));hash.AppendData(row.Bytes);}
    var legacy=await tx.QueryAsync("SELECT v FROM (SELECT 'version:'||set_id||':'||version||':'||body::text AS v FROM reference_configuration.parameter_set_version UNION ALL SELECT 'assignment:'||assignment_id||':'||body::text FROM reference_configuration.parameter_assignment_revision UNION ALL SELECT 'run:'||run_id||':'||active||':'||body::text FROM reference_configuration.parameter_startup_run UNION ALL SELECT 'report:'||run_id||':'||revision||':'||body::text FROM reference_configuration.parameter_startup_report UNION ALL SELECT 'audit:'||operation_id||':'||body::text FROM reference_configuration.parameter_set_audit) t ORDER BY v;",[],r=>r.GetString(0),ct);
    var legacyHash=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",legacy))));
    return new SourceProof(parts.Count,parts.Select(x=>x.Version).DefaultIfEmpty().Max(),Convert.ToHexStringLower(hash.GetHashAndReset()),legacyHash);
},token);
sealed record SourceProof(int Events,long MaximumVersion,string SourceSha256,string LegacyProjectionSha256);
sealed record LegacyRows(IReadOnlyList<ParameterSetVersion> Versions,IReadOnlyList<ParameterAssignmentRevision> Assignments,IReadOnlyList<ParameterStartupRun> Runs,IReadOnlyList<ParameterSignalStartupReport> Reports);
sealed class CutoverResolver(IDbConnectionSettings settings):IDbContextResolver
{
    readonly Dictionary<Type,object> contexts=[];
    public IDbContextFactory Factory=>new DbContextFactory(this);
    public IObjectRepository<TRepo> Resolve<TRepo>() where TRepo:IObjectRepository
    {
        if(!contexts.TryGetValue(typeof(TRepo),out var value))
        {
            // This offline tool uses only the configuration/catalog methods; no market-data providers are started.
            value=typeof(TRepo)==typeof(ConfigurationDbContext)?new ConfigurationDbContext(settings,Factory,NullLogger<DbProvider>.Instance):
                typeof(TRepo)==typeof(MarketDataDbContext)?new MarketDataDbContext(settings,Factory,new OfflineBlackboard(),new OfflineSequences(),NullLogger<DbProvider>.Instance):
                throw new NotSupportedException(typeof(TRepo).Name);
            contexts.Add(typeof(TRepo),value);
        }
        return (IObjectRepository<TRepo>)value;
    }
}


// These capabilities are deliberately unavailable: catalog migration must never query feeds or allocate business IDs.
sealed class OfflineBlackboard:TomasAI.IFM.Application.Blackboard.IBlackboardService
{
    public TomasAI.IFM.Application.Blackboard.IEventSourcingBlackboard EventSourcing=>throw new NotSupportedException();
    public TomasAI.IFM.Application.Blackboard.IMarketDataBlackboard MarketData=>throw new NotSupportedException();
    public TomasAI.IFM.Application.Blackboard.IMarketDataAnalyticsBlackboard MarketDataAnalytics=>throw new NotSupportedException();
    public TomasAI.IFM.Application.Blackboard.IMarketDataFeedBlackboard MarketDataFeed=>throw new NotSupportedException();
    public TomasAI.IFM.Application.Blackboard.IMarketDataSecuritiesBlackboard MarketDataSecurities=>throw new NotSupportedException();
    public TomasAI.IFM.Application.Blackboard.IReferenceBlackboard Reference=>throw new NotSupportedException();
    public TomasAI.IFM.Application.Blackboard.ITradeBlackboard Trade=>throw new NotSupportedException();
}
sealed class OfflineSequences:TomasAI.IFM.Framework.SequenceId.ISequenceIdGenerator
{
    public ValueTask<long> GetSequenceIdAsync(TomasAI.IFM.Framework.SequenceId.SequenceName name,CancellationToken token=default)=>throw new NotSupportedException();
    public ValueTask<long> GetHighWatermarkAsync(TomasAI.IFM.Framework.SequenceId.SequenceName name,CancellationToken token=default)=>throw new NotSupportedException();
}
