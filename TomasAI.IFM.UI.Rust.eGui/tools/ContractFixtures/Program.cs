// Development-only compatibility oracle. This tool is never packaged with the UI.
using System.Reflection;
using System.Text.Json;
using NATS.Client.Core;
using MessagePack;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Domain.Reference.Shared.Events;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;

if(args.Length==4&&args[0]=="--control"){
 var uri=new Uri(args[1]);if(!uri.IsLoopback||uri.Port==4222||args[2].Length==0||!args[2].All(char.IsAsciiLetterOrDigit))throw new InvalidOperationException("Control requires isolated loopback fixtures");
 await using var connection=new NatsConnection(new NatsOpts{Url=args[1],Name="IFM isolated desktop control"});await connection.ConnectAsync();using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(10));
 var reply=await connection.RequestAsync<byte[],byte[]>("Query.NativeCompatibility."+args[2]+".none",MessagePackSerializer.Serialize(args[3],MessagePackBinarySerializer.Options),cancellationToken:deadline.Token);
 var document=MessagePackSerializer.ConvertToJson(reply.Data!,MessagePackBinarySerializer.Options);Console.WriteLine(document);using var parsed=JsonDocument.Parse(document);if(!parsed.RootElement[0].GetBoolean())throw new InvalidOperationException("Isolated desktop control failed: "+document);return;
}
if (args.Length is < 1 or > 3) throw new ArgumentException("Supply fixture directory, optional Rust output directory, or --serve URL.");
// Select the opt-in fixture clock before any exported static sample is built.
DesktopFixtureMode.Enabled=args.Length==3&&args[1]=="--desktop-serve";
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var entries = new List<object>();
void Write<T>(string name, T value)
{
    var raw = MessagePackSerializer.Serialize(value, MessagePackBinarySerializer.ContentOptions);
    var compressed = MessagePackSerializer.Serialize(value, MessagePackBinarySerializer.Options);
    FixtureFiles.WriteAllBytes(Path.Combine(output, name + ".raw.msgpack"), raw);
    FixtureFiles.WriteAllBytes(Path.Combine(output, name + ".wire.msgpack"), compressed);
    entries.Add(new { name, type = typeof(T).AssemblyQualifiedName, rawLength = raw.Length, wireLength = compressed.Length });
    if (args.Length == 2)
    {
        var rust = File.ReadAllBytes(Path.Combine(args[1], name + ".msgpack"));
        var decoded = MessagePackSerializer.Deserialize<T>(rust, MessagePackBinarySerializer.Options);
        var reencoded = MessagePackSerializer.Serialize(decoded, MessagePackBinarySerializer.ContentOptions);
        if (!raw.AsSpan().SequenceEqual(reencoded)) throw new InvalidDataException($"Rust/C# typed roundtrip differs: {name}");
    }
}
var date = new DateOnly(2026, 10, 26);
Write("strategy-signal-history-query",new TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries.GetFuturesItiSignalHistoryQuery("ES",date,TimeFrameType.Daily){Subject=new(ActorType.Query,"FuturesItiSignalQuery","GetFuturesItiSignalHistory","ES.2026-10-26.Daily")});
BackupFixtures.Export(output);
ParameterFixtures.Export(output);
await ParameterSessionFixtures.Export(output);
StrategyFixtures.Export(output);
InspectionFixtures.Export(output);
PortfolioFixtures.Export(output);
FinancialFixtures.Export(output);
FinancialJournalFixtures.Export(output);
if(args.Length==2)await FinancialJournalFixtures.Verify(args[1]);
await PortfolioSessionFixtures.Export(output);
await TradeWorkspaceFixtures.Export(output);
OptionSelectionFixtures.Export(output);
OptionDraftFixtures.Export(output);
BrokerPreviewDisplayFixtures.Export(output);
BrokerMarketDisplayFixtures.Export(output);
Write("backup-operation",BackupFixtures.Operation);Write("backup-policy",BackupFixtures.Policy);Write("backup-protection",BackupFixtures.Protection);Write("backup-setup",BackupFixtures.Setup);
await ScheduledFixtures.Export(output);
Write("scheduled-schedule",ScheduledFixtures.Schedule);Write("scheduled-run",ScheduledFixtures.Run);Write("scheduled-dashboard",ScheduledFixtures.Dashboard);
await ActorHealthFixtures.Export(output);
MarketHistoryFixtures.Export(output);
OutlookFixtures.Export(output);
SidebarFixtures.Export(output);
var definitionFuture=new InstrumentDefinitionSelection{SnapshotId=Guid.Parse("721dd687-7a5a-4fa6-a404-01545cbab900"),Dataset="GLBX.MDP3",PublisherId=1,InstrumentId=1122,RawSymbol="ESZ6",Root="ES",InstrumentClass="F",Currency="USD",Exchange="CME",Multiplier=50.00m,TickSize=0.25m,ExpirationUtc=new(2026,12,18,0,30,0,TimeSpan.FromHours(2)),DefinitionTimestampUtc=new(2026,10,24,9,0,0,TimeSpan.FromHours(-4)),DefinitionDigest=new('a',64),RawDefinitionReference="fixture/future"};
var definitionAt=new DateTimeOffset(2026,10,24,12,0,0,TimeSpan.Zero);
var mappedFuture=TomasAI.IFM.UI.Rust.Bridge.Net.ProviderReferenceMapping.Future(definitionFuture,"America/Chicago",definitionAt);
var definitionOption=definitionFuture with{InstrumentId=3344,RawSymbol="ESZ6 C6500.25",Root="",InstrumentClass="C",UnderlyingInstrumentId=1122,Strike=6500.2500m,ExpirationUtc=new(2026,11,20,0,30,0,TimeSpan.FromHours(2)),RawDefinitionReference="fixture/option"};
Write("definition-future",definitionFuture);Write("definition-option",definitionOption);Write("definition-mapped-future",mappedFuture);
Write("definition-mapped-option",TomasAI.IFM.UI.Rust.Bridge.Net.ProviderReferenceMapping.Option(definitionOption,mappedFuture,"America/Chicago",definitionAt));
FixtureFiles.WriteAllText(Path.Combine(output,"definition-future-preview.json"),JsonSerializer.Serialize(TomasAI.IFM.UI.Rust.Bridge.Net.BridgeDocument.Project(mappedFuture,camelCase:true)));
FixtureFiles.WriteAllText(Path.Combine(output,"definition-option-preview.json"),JsonSerializer.Serialize(TomasAI.IFM.UI.Rust.Bridge.Net.BridgeDocument.Project(TomasAI.IFM.UI.Rust.Bridge.Net.ProviderReferenceMapping.Option(definitionOption,mappedFuture,"America/Chicago",definitionAt),camelCase:true)));
var existingMappedFuture=mappedFuture with{ContractId="ESZ6-provider",OnTheRun=true,Rollover=true,MappingVersion="kept-map",LastTradingUtc=new(2026,12,17,15,0,0,TimeSpan.FromHours(-6))};
var existingMappedOption=TomasAI.IFM.UI.Rust.Bridge.Net.ProviderReferenceMapping.Option(definitionOption,mappedFuture,"America/Chicago",definitionAt) with{SchemaVersion=0,StrikePriceDecimal=null,MappingVersion="kept-option-map",ExerciseCutoffUtc=new(2026,11,19,15,0,0,TimeSpan.FromHours(-6))};
Write("definition-existing-future",existingMappedFuture);Write("definition-existing-option",existingMappedOption);
Write("definition-remapped-future",TomasAI.IFM.UI.Rust.Bridge.Net.ProviderReferenceMapping.Future(definitionFuture,"America/Chicago",definitionAt,existingMappedFuture));
Write("definition-remapped-option",TomasAI.IFM.UI.Rust.Bridge.Net.ProviderReferenceMapping.Option(definitionOption,mappedFuture,"America/Chicago",definitionAt,existingMappedOption));
Write("supervisor-restart",new TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands.RestartSupervisorActorCommand {
    CommandId=Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),Subject=new(ActorType.Command,"SupervisorCommand","RestartActor","none"),
    Target=new(ActorType.Realtime,"IronCondorPosition","101.701.1901.1501"),ExpectedGeneration=17,Requester="Fixture operator",Reason="Offline native parity",TimeoutTicks=TimeSpan.FromMinutes(2).Ticks
});
Write("value-date-query", new GetValueDateQuery { Subject = new(ActorType.Query, "MarketDataQuery", "GetValueDate", "valueDate") });
Write("market-session-query", new GetMarketSessionQuery { Subject = new(ActorType.Query, "MarketDataQuery", "GetMarketSession", "marketSession") });
Write("value-date-result", new ServiceResult<ScalarReadModel<DateOnly>> { Success = true, ErrorMessage = "", Value = new(date) });
Write("error-result", new ServiceResult<ScalarReadModel<DateOnly>> { Success = false, ErrorCode = 1015, ErrorMessage = "Server clock unavailable" });
Write("command-guid-result",new ServiceResult<GuidResult>{Success=true,ErrorMessage="",Value=new(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"))});
Write("market-session-result", new ServiceResult<MarketSessionReadModel> { Success = true, ErrorMessage = "", Value = new() {
    OperationalValueDate = date, ActiveValueDate = date, Revision = 42,
    SessionStartUtc = new(2026,10,25,22,0,0,DateTimeKind.Utc), SessionEndUtc = new(2026,10,26,21,0,0,DateTimeKind.Utc),
    NextTransitionUtc = new(2026,10,26,21,0,0,DateTimeKind.Utc), AsOfUtc = new(2026,10,26,12,30,0,DateTimeKind.Utc),
    MarketTime = new(2026,10,26,8,30,0), State = FuturesMarketState.LiveTrading
} });
Write("decimal", 12345678901234567890.123456789m);
Write("negative-decimal", -0.0000000000000000000000000001m);
Write("date-only", date);
Write("time-only", new TimeOnly(12,30,0).Add(TimeSpan.FromTicks(1234567)));
var providerContract=new FuturesContractV3ReadModel{ContractId="ESZ6",Symbol="ES",Description="Provider fixture",LocalSymbol="ESZ6",SecurityType="FUT",Currency="USD",Exchange="CME",Multiplier="50",LastTradeDate=new(2026,12,18),OnTheRun=true,Rollover=true,SchemaVersion=1,ReviewState=ReferenceReviewState.Draft,Dataset="GLBX.MDP3",PublisherId=1,InstrumentId=123,DefinitionTimestampUtc=new(2026,10,26,12,30,0,TimeSpan.Zero),DefinitionDigest="immutable-definition-digest",RawDefinitionReference="immutable-evidence-location",EvidenceId="immutable-evidence",TickSize=0.25m};
Write("provider-contract",providerContract);
Write("provider-contract-patched",providerContract with{Description="Reviewed fixture",ReviewState=ReferenceReviewState.Reviewed,TickSize=0.1250m,ExpirationUtc=new(2026,12,18,8,30,0,TimeSpan.FromHours(-4))});
var reviewDisplayContract=providerContract with{EffectiveFromUtc=new(2026,10,26,8,30,0,TimeSpan.FromHours(-4)),EffectiveUntilUtc=new(2026,12,18,21,0,0,TimeSpan.Zero),LastTradingUtc=new(2026,12,18,20,0,0,TimeSpan.Zero)};
Write("provider-contract-review-display",reviewDisplayContract);
FixtureFiles.WriteAllText(Path.Combine(output,"contract-review-display.json"),JsonSerializer.Serialize(TomasAI.IFM.UI.Rust.Bridge.Net.ReferenceReviewMapping.Row(new TomasAI.IFM.UI.Rust.Bridge.Net.ContractEditorRow("fixture",new()),reviewDisplayContract).Fields));
var strikeSamples=new[]{"6500.2500","0006500.25","10000","0.0000000000000000000000000001","79228162514264337593543950335","1.00000000000000000000000000000"};
FixtureFiles.WriteAllText(Path.Combine(output,"option-identity.json"),JsonSerializer.Serialize(strikeSamples.Select(text=>{if(!FuturesOptionContractId.TryParseStrike(text,out var strike))throw new InvalidDataException(text);return new{text,payload=strike.ToString(System.Globalization.CultureInfo.InvariantCulture),id=FuturesOptionContractId.Create("ES",date,OptionType.Put,strike),legacy=(double)strike};})));
var decimalRandom=new Random(741);var decimalCases=new List<object>();
for(byte scale=0;scale<=28;scale++)for(int sample=0;sample<8;sample++){
    var value=new decimal(unchecked((int)decimalRandom.NextInt64(1L<<32)),unchecked((int)decimalRandom.NextInt64(1L<<32)),unchecked((int)decimalRandom.NextInt64(1L<<32)),sample%2==0,scale);
    decimalCases.Add(new{text=value.ToString(System.Globalization.CultureInfo.InvariantCulture),bits=BitConverter.DoubleToUInt64Bits((double)value)});
}
FixtureFiles.WriteAllText(Path.Combine(output,"decimal-legacy-double.json"),JsonSerializer.Serialize(decimalCases));
var optionFields=new Dictionary<string,string>{{"symbol","ES"},{"description","Legacy option"},{"localSymbol","ES fixture"},{"securityType","FOP"},{"currency","USD"},{"exchange","CME"},{"multiplier","50"},{"contractMonth","2026-10-26"},{"optionType","Put"},{"strikePrice","6500.2500"}};
var optionInput=new TomasAI.IFM.UI.Rust.Bridge.Net.ContractEditorIntent(1,Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),"option-contract","add",0,null,optionFields);
var legacyOption=TomasAI.IFM.UI.Rust.Bridge.Net.ContractDraftMapping.Option(optionInput,null);
var providerOption=legacyOption with{SchemaVersion=1,ReviewState=ReferenceReviewState.Draft,Dataset="GLBX.MDP3",PublisherId=1,InstrumentId=456,RawSymbol="Fixture Put",DefinitionDigest="immutable-option-digest",RawDefinitionReference="immutable-option-evidence",UnderlyingContractId="ESZ6",UnderlyingAssetType=ReferenceAssetType.Futures,OptionRight=ReferenceOptionRight.Put,TickSize=0.25m};
Write("option-legacy-draft",legacyOption);Write("option-provider",providerOption);
var changedOptionFields=new Dictionary<string,string>(optionFields){{"onTheRun","false"}};changedOptionFields["description"]="Edited option";changedOptionFields["symbol"]="EVIL";changedOptionFields["strikePrice"]="not-a-price";
Write("option-provider-edited",TomasAI.IFM.UI.Rust.Bridge.Net.ContractDraftMapping.Option(optionInput with{Fields=changedOptionFields},providerOption));
FixtureFiles.WriteAllText(Path.Combine(output,"option-draft-inputs.json"),JsonSerializer.Serialize(new{legacy=optionFields,provider=changedOptionFields}));
FixtureFiles.WriteAllText(Path.Combine(output,"option-review-display.json"),JsonSerializer.Serialize(TomasAI.IFM.UI.Rust.Bridge.Net.ReferenceReviewMapping.Row(new TomasAI.IFM.UI.Rust.Bridge.Net.ContractEditorRow("fixture",new()),providerOption).Fields));
Write("datetime", new DateTime(2026,10,26,12,30,0,DateTimeKind.Utc).AddTicks(1234567));
Write("datetime-offset", new DateTimeOffset(2026,10,26,8,30,0,TimeSpan.FromHours(-4)));
Write("timespan", TimeSpan.FromTicks(-123456789));
Write("guid", Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));
Write<IActorEntityId>("actor-entity-union",ActorEntityId.Default);
Write("status-log-map",new StatusConsoleLogReadModel(new(2026,10,26,12,30,0,DateTimeKind.Utc),503,LogSourceType.MarketDataFeed,"Recovery deadline expired","Options","GLBX.MDP3"));
Write("status-notification",new TomasAI.IFM.Shared.StatusConsole.Events.StatusConsoleLoggedEvent {
    Subject=new(ActorType.Notify,"StatusConsoleEvent","Logged","none"),Id=Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),EntityId=ActorEntityId.Default,
    // The C# serialization constructor normalizes these strings to empty. Use the
    // canonical event shape so byte comparison tests transport, not normalization.
    AggregateId="",EventSource="",
    ReceivedOn=new(2026,10,26,12,30,0,DateTimeKind.Utc),StatusConsoleLog=new(new(2026,10,26,12,30,0,DateTimeKind.Utc),0,LogSourceType.MarketDataFeed,"Option subscriptions restored")
});
Write("lookup-query", new GetLookupTypesQuery { Subject = new(ActorType.Query, "LookupTypeQuery", "GetLookupTypes", "all") });
Write("lookup-result", new ServiceResult<LookupTypeCollection> { Success = true, ErrorMessage = "", Value = new([new LookupTypeReadModel("Currency", "USD", 1, "US Dollar", new(2026,10,23,12,0,0,DateTimeKind.Utc), "Fixture")]) });
Write("feed-status-query", new GetMarketDataFeedRuntimeStatusQuery { Subject = new(ActorType.Query, "MarketDataFeedQuery", "GetRuntimeStatus", "runtime-status") });
Write("feed-status-result", new ServiceResult<MarketDataFeedRuntimeStatusReadModel> { Success = true, ErrorMessage = "", Value = new() {
    IsRunning = true, ActiveValueDate = date, ObservedAtUtc = new(2026,10,26,12,30,0,TimeSpan.Zero),
    EnabledOptionTrades = ["101.701.1901.1501"], OptionProcessingError = "Option pipeline recovery failed: The two-minute recovery deadline expired."
} });
Write("recovery-failure-event", new MarketDataDatasetRecoveryFailedEvent {
    Subject = new(ActorType.Event, "MarketDataFeedEvent", "DatasetRecoveryFailed", "2026-10-26"), EntityId = new MarketDataFeedId(date),
    CommandId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"), Id = Guid.Parse("ffeeddcc-bbaa-9988-7766-554433221100"),
    ErrorDate = new(2026,10,26,12,30,0,DateTimeKind.Utc), ReceivedOn = new(2026,10,26,12,30,0,DateTimeKind.Utc),
    ErrorMessage = "Option pipeline recovery failed: The two-minute recovery deadline expired.", ErrorCode = 503,
    Dataset = "GLBX.MDP3", Role = "Options", EventSource = "Compatibility fixture"
});
Write("compressed-large", Enumerable.Range(0, 20000).Select(i => $"contract-{i % 7:D2}-" + new string('x', 32)).ToArray());
foreach(var failed in new[]{false,true})Write(failed?"daily-analytics-failure-event":"daily-analytics-ready-event",new DailyAnalyticsInitializationStatusEvent{
    Subject=new(ActorType.Event,"MarketDataFeedEvent","DailyAnalyticsInitializationStatus","2026-10-26"),EntityId=new MarketDataFeedId(date),Id=Guid.Parse(failed?"a1b20000-0000-0000-0000-000000000001":"a1b20000-0000-0000-0000-000000000002"),CommandId=Guid.Parse(failed?"a1b20000-0000-0000-0000-000000000003":"a1b20000-0000-0000-0000-000000000004"),ReceivedOn=new(2026,10,26,12,30,0,DateTimeKind.Utc),ErrorDate=new(2026,10,26,12,30,0,DateTimeKind.Utc),RefreshFailed=failed,ErrorMessage=failed?"Daily indicator refresh failed":"Daily indicators ready",BaselineValueDate=date
});
var hashValues = new object[] {
    new { Z = 1.2300m, A = "Unicode é Ω 😀 <>&'\"+`\\\n", Tiny = 0.00001m },
    new { DecimalMax = decimal.MaxValue, DecimalMin = decimal.MinValue, Smallest = 0.0000000000000000000000000001m, Zero = 0.000m, ExponentBoundary = 0.0001m },
    new { OperationId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"), RequestedAtUtc = new DateTime(2026,10,26,12,30,0,DateTimeKind.Utc).AddTicks(1234567), Values = new decimal[] { 1, -2.5000m, 0.0000012300m } }
};
FixtureFiles.WriteAllText(Path.Combine(output, "hashes.json"), JsonSerializer.Serialize(hashValues.Select(value => new {
    json = JsonSerializer.SerializeToElement(value), hash = FinancialCanonicalHash.Compute(value)
}), new JsonSerializerOptions { WriteIndented = true }));
FixtureFiles.WriteAllText(Path.Combine(output, "catalog-defaults.json"), StrategyCatalogJson.Write(StrategyCatalogDefaults.Create()));
Write("catalog-query", new StrategyCatalogQuery {
    Subject = new(ActorType.Query, "ReferenceQuery", "StrategyCatalog", "none"),
    RequestJson = StrategyCatalogJson.Write(new CatalogQueryRequest(CatalogQueryOperation.List))
});
Write("catalog-command", new StrategyCatalogCommand {
    CommandId = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"),
    Subject = new(ActorType.Command, "TradeStrategyFamilyCommand", "Catalog", "none"),
    RequestJson = StrategyCatalogJson.Write(new CatalogCommandRequest(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"), CatalogCommandOperation.SaveDraft,
        Definition: StrategyCatalogDefaults.Create()[0], ExpectedPreviousVersion: 0))
});
// Include the catalog wire fixtures added after the primitive set above.
FixtureFiles.WriteAllText(Path.Combine(output, "fixtures.json"), JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));

// Reflect actual compiled client contracts rather than inferring MessagePack keys from names.
var visited = new HashSet<string>();
var assemblies = new Queue<Assembly>();
assemblies.Enqueue(typeof(MarketDataQueryApi).Assembly);
var types = new List<Type>();
while (assemblies.TryDequeue(out var assembly))
{
    if (!visited.Add(assembly.FullName!)) continue;
    types.AddRange(assembly.GetExportedTypes());
    foreach (var reference in assembly.GetReferencedAssemblies().Where(a => a.Name!.StartsWith("TomasAI.IFM.", StringComparison.Ordinal)))
        assemblies.Enqueue(Assembly.Load(reference));
}
// Framework enums used by typed domain fields are wire contracts too.
types.AddRange([typeof(DayOfWeek),typeof(DateTimeKind)]);
var namedModels=types.Where(t=>t.Namespace=="TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels").ToHashSet();
bool Named(Type type)=>type.GetCustomAttribute<MessagePackObjectAttribute>()?.KeyAsPropertyName==true||namedModels.Contains(type)&&type.GetCustomAttribute<MessagePackObjectAttribute>() is null;
var schemas = types.Distinct().Where(t => t.GetCustomAttribute<MessagePackObjectAttribute>() != null || t.IsEnum || t.GetCustomAttributes<UnionAttribute>().Any() || namedModels.Contains(t))
    .OrderBy(t => t.FullName).Select(t => new {
        type = t.FullName, isEnum = t.IsEnum,
        errorId = t.GetField("ErrorId", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetRawConstantValue(),
        mapByName = Named(t),
        genericParameters = t.IsGenericTypeDefinition ? t.GetGenericArguments().Select(a=>a.Name).ToArray() : [],
        unions = t.GetCustomAttributes<UnionAttribute>().Select(u=>new{tag=u.Key,type=u.SubType.ToString()}).ToArray(),
        enumValues = t.IsEnum ? Enum.GetNames(t).Select(n => new { name = n, value = Convert.ToInt64(Enum.Parse(t, n)) }).ToArray() : null,
        members = t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(m => new { member = m, key = m.GetCustomAttribute<KeyAttribute>() })
            .Where(m => m.member.GetCustomAttribute<IgnoreMemberAttribute>() == null && (m.key != null ||
                Named(t) &&
                (m.member is PropertyInfo {GetMethod.IsPublic:true} p && p.GetIndexParameters().Length==0 || m.member is FieldInfo {IsPublic:true})))
            .Select(m => new {
                name = m.member.Name, index = Named(t) ? null : m.key?.IntKey,
                mapKey = Named(t) ? m.member.Name : m.key?.StringKey,
                type = (m.member is PropertyInfo p ? p.PropertyType : ((FieldInfo)m.member).FieldType).ToString()
            }).ToArray(),
        actor = t.GetField("Actor", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetRawConstantValue(),
        verb = t.GetField("Verb", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)?.GetRawConstantValue()
    }).ToArray();
FixtureFiles.WriteAllText(Path.Combine(output, "contracts.json"), JsonSerializer.Serialize(schemas, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Exported {entries.Count} fixtures and {schemas.Length} compiled contract schemas to {output}");
OperationInventory.Export(output);
await ComposerSessionFixtures.Export(output,providerContract);
MonitorFixtures.Export(output);
if (args.Length == 3 && args[1] is "--serve" or "--desktop-serve")
{
    var uri = new Uri(args[2]);
    if (!uri.IsLoopback || uri.Port == 4222) throw new InvalidOperationException("Compatibility responder requires an isolated loopback NATS port, not the Development broker.");
    await using var connection = new NatsConnection(new NatsOpts { Url = args[2], Name = "IFM standalone C# compatibility test" });
    await connection.ConnectAsync();
    DesktopFixtureMode.Enabled=args[1]=="--desktop-serve";
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(DesktopFixtureMode.Enabled?30:5));
    var desktopHttp=DesktopFixtureMode.Enabled?DesktopFixtureMode.ServeHttp(output,timeout.Token):Task.CompletedTask;
    if(desktopHttp.IsFaulted)await desktopHttp;
    var catalog = StrategyCatalogDefaults.Create().ToDictionary(d => d.Key, d => new StoredStrategyCatalogDefinition(d, FinancialCanonicalHash.Compute(d), CatalogLifecycleStatus.Draft,
        new(2026,10,23,12,0,0,DateTimeKind.Utc), "Fixture", null, null, null, null));
    var catalogOperations = new Dictionary<Guid,string>();
    var lookupRows = new List<LookupTypeReadModel> {new("Currency","USD",1,"US Dollar",new(2026,10,23,12,0,0,DateTimeKind.Utc),"Fixture")};
    foreach(var (type,code) in new[]{("Symbol","ES"),("SecurityType","FUT"),("Exchange","CME"),("Multiplier","50")})lookupRows.Add(new(type,code,1,code,new(2026,10,23,12,0,0,DateTimeKind.Utc),"Fixture"));
    var futuresContracts=new FuturesContractResponder(providerContract);
    var optionContracts=new OptionContractResponder(providerOption);
    var definitionMode="Normal";
    var scheduledTasks=new ScheduledResponder();
    var backupAdmin=new BackupResponder();
    var parameterAdmin=new ParameterResponder();
    var strategies=new StrategyResponder();
    var inspections=new InspectionResponder();
    var portfolios=new PortfolioResponder();
    var tradeWorkspace=new TradeWorkspaceResponder();
    var financial=new FinancialResponder();
    var financialOperations=new FinancialOperationResponder();
    foreach(var (type,code) in new[]{("Symbol","MES"),("SecurityType","FOP"),("OptionType","Call"),("OptionType","Put")})lookupRows.Add(new(type,code,2,code,new(2026,10,23,12,0,0,DateTimeKind.Utc),"Fixture"));
    MonitorResponder.Session=MessagePackSerializer.Deserialize<ServiceResult<MarketSessionReadModel>>(File.ReadAllBytes(Path.Combine(output,"market-session-result.wire.msgpack")),MessagePackBinarySerializer.Options).Value!;
    var feedRuntime=MessagePackSerializer.Deserialize<ServiceResult<MarketDataFeedRuntimeStatusReadModel>>(File.ReadAllBytes(Path.Combine(output,"feed-status-result.wire.msgpack")),MessagePackBinarySerializer.Options).Value!;
    // The exported failure sample is for recovery tests. Ordinary desktop
    // navigation starts healthy; failure-dialog scenarios inject it explicitly.
    if(DesktopFixtureMode.Enabled)feedRuntime=feedRuntime with{OptionProcessingError=""};
    var feedScenario="Normal";
    var holdCalendar=false;
    var outlookAvailable=false;
    var calendarResponder = new CalendarResponder();
    var yieldRows = new Dictionary<DateOnly,YieldCurveRateReadModel>();
    await using var subscription = await connection.SubscribeCoreAsync<byte[]>(">", cancellationToken: timeout.Token);
    await connection.PingAsync(timeout.Token);
    Console.WriteLine("READY: isolated compatibility responder subscribed");
    await foreach (var request in subscription.Msgs.ReadAllAsync(timeout.Token))
    {
        if (string.IsNullOrEmpty(request.ReplyTo)) continue;
        if(DesktopFixtureMode.Enabled)Console.WriteLine("REQUEST: "+request.Subject);
        byte[]? response = null;
        if(EodResponder.Handles(request.Subject)){response=EodResponder.Handle(request.Subject,request.Data!);}
        else if(CloseOrderResponder.Handles(request.Subject)){response=CloseOrderResponder.Handle(request.Subject,request.Data!);}
        else if(MonitorPublicationResponder.Handles(request.Subject)){
            response=await MonitorPublicationResponder.Handle(connection,request.Data!,timeout.Token);
        }
        else if(MonitorResponder.Handles(request.Subject)){
            response=await MonitorResponder.HandleAsync(connection,request.Subject,request.Data!,request.ReplyTo!,timeout.Token);
        }
        else if(TradeLifecycleResponder.Handles(request.Subject)){
            response=TradeLifecycleResponder.Handle(request.Subject,request.Data!);
        }
        else if(OrderPreparationResponder.Handles(request.Subject)){
            response=OrderPreparationResponder.Handle(request.Subject,request.Data!);
        }
        else if(BrokerOrderCommandResponder.Handles(request.Subject)){
            response=await BrokerOrderCommandResponder.Handle(connection,request.Subject,request.Data!,timeout.Token);
        }
        else if(BrokerAccountCommandResponder.Handles(request.Subject)){
            response=await BrokerAccountCommandResponder.Handle(connection,request.Subject,request.Data!,timeout.Token);
        }
        else if(BrokerInitializationResponder.Handles(request.Subject)){
            response=BrokerInitializationResponder.Handle(request.Subject,request.Data!);
        }
        else if(BrokerReadResponder.Handles(request.Subject)){
            response=BrokerReadResponder.Handle(request.Subject,request.Data!);
        }
        else if(OptionChainResponder.Handles(request.Subject)){
            response=OptionChainResponder.Handle(request.Subject,request.Data!);
        }
        else if(request.Subject=="Query.NativeCompatibility.TradeWorkspaceMode.none"){
            var tradeMode=MessagePackSerializer.Deserialize<string>(request.Data!,MessagePackBinarySerializer.Options);
            if(tradeMode is "Normal" or "ComposeFutures" or "ComposeOptions" or "ComposeCloseFutures" or "Monitor")futuresContracts.Reset();
            tradeWorkspace.Mode=tradeMode;
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject=="Query.NativeCompatibility.TradeWorkspaceCount.none"){
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<int>>{Success=true,ErrorMessage="",Value=new(tradeWorkspace.Dispatches)},MessagePackBinarySerializer.Options);
        }
        else if(tradeWorkspace.Handles(request.Subject)){
            response=tradeWorkspace.Handle(request.Subject,request.Data!);
        }
        else if(request.Subject.StartsWith("Query.GeneralLedgerQuery.GetPostingReceipt.",StringComparison.Ordinal)||request.Subject.StartsWith("Command.GeneralLedgerCommand.",StringComparison.Ordinal)||request.Subject.StartsWith("Command.LedgerConfigurationCommand.",StringComparison.Ordinal)){
            response=financialOperations.Handle(request.Subject,request.Data!);
        }
        else if(request.Subject=="Query.NativeCompatibility.FinancialOperationMode.none"){
            financialOperations.Mode=MessagePackSerializer.Deserialize<string>(request.Data!,MessagePackBinarySerializer.Options);
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject=="Query.NativeCompatibility.FinancialOperationCount.none"){
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<int>>{Success=true,ErrorMessage="",Value=new(financialOperations.Dispatches)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject.StartsWith("Query.GeneralLedgerQuery.",StringComparison.Ordinal)||request.Subject.StartsWith("Query.CapacityReservationQuery.",StringComparison.Ordinal)){
            response=financial.Handle(request.Subject,request.Data!);
        }
        else if(request.Subject=="Query.NativeCompatibility.FinancialMode.none"){
            financial.Mode=MessagePackSerializer.Deserialize<string>(request.Data!,MessagePackBinarySerializer.Options);
            financialOperations.LedgerMode=financial.Mode;
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject.StartsWith("Command.PortfolioFinancialPolicyCommand.",StringComparison.Ordinal)){
            response=portfolios.Command(request.Subject,request.Data!);
        }
        else if(request.Subject.StartsWith("Command.PortfolioCommand.",StringComparison.Ordinal)||request.Subject.StartsWith("Command.PortfolioFundCommand.",StringComparison.Ordinal)){
            response=portfolios.AdminCommand(request.Subject,request.Data!);
        }
        else if(request.Subject.StartsWith("Query.PortfolioQuery.",StringComparison.Ordinal)||request.Subject.StartsWith("Query.PortfolioFundQuery.",StringComparison.Ordinal)||request.Subject.StartsWith("Query.PortfolioFinancialPolicyQuery.",StringComparison.Ordinal)){
            response=portfolios.Handle(request.Subject,request.Data!);
        }
        else if(request.Subject=="Query.NativeCompatibility.PortfolioMode.none"){
            portfolios.Mode=MessagePackSerializer.Deserialize<string>(request.Data!,MessagePackBinarySerializer.Options);
            if(portfolios.Mode=="Reset")portfolios.Reset();
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject.StartsWith("Query.RiskManagementQuery.",StringComparison.Ordinal)||request.Subject.StartsWith("Query.MarketConditionPipelineQuery.",StringComparison.Ordinal)){
            response=inspections.Handle(request.Subject,request.Data!);
        }
        else if(request.Subject=="Query.NativeCompatibility.InspectionMode.none"){
            inspections.Mode=MessagePackSerializer.Deserialize<string>(request.Data!,MessagePackBinarySerializer.Options);
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject.StartsWith("Query.IntrinsicTimeStrategyWorkflowQuery.",StringComparison.Ordinal)||request.Subject.StartsWith("Query.FuturesItiSignalQuery.GetFuturesItiSignalHistory.",StringComparison.Ordinal)||request.Subject=="Query.NativeCompatibility.StrategyEmit.none"){
            response=await strategies.Handle(connection,request.Subject,request.Data!,timeout.Token);
        }
        else if(request.Subject=="Query.NativeCompatibility.StrategyMode.none"){
            strategies.Mode=MessagePackSerializer.Deserialize<string>(request.Data!,MessagePackBinarySerializer.Options);
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject.StartsWith("Query.ParameterSetQuery.",StringComparison.Ordinal)||request.Subject.StartsWith("Command.ParameterSetCommand.",StringComparison.Ordinal)||request.Subject.StartsWith("Command.ParameterAssignmentCommand.",StringComparison.Ordinal)){
            response=await parameterAdmin.Handle(connection,request.Subject,request.Data!,timeout.Token);
        }
        else if(request.Subject=="Query.NativeCompatibility.ParameterMode.none"){
            parameterAdmin.Mode=MessagePackSerializer.Deserialize<string>(request.Data!,MessagePackBinarySerializer.Options);
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject.StartsWith("Query.DatabaseBackupQuery.",StringComparison.Ordinal)||request.Subject.StartsWith("Command.DatabaseBackupCommand.",StringComparison.Ordinal)){
            response=await backupAdmin.Handle(connection,request.Subject,request.Data!,timeout.Token);
        }
        else if(request.Subject=="Query.NativeCompatibility.BackupMode.none"){
            backupAdmin.Mode=MessagePackSerializer.Deserialize<string>(request.Data!,MessagePackBinarySerializer.Options);
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject.StartsWith("Query.ScheduledTaskQuery.",StringComparison.Ordinal)||request.Subject.StartsWith("Command.ScheduledTaskCommand.",StringComparison.Ordinal)||request.Subject.StartsWith("Command.ScheduledTaskRunCommand.",StringComparison.Ordinal)){
            response=await scheduledTasks.Handle(connection,request.Subject,request.Data!,timeout.Token);
        }
        else if(request.Subject=="Query.NativeCompatibility.ScheduledMode.none"){
            scheduledTasks.Mode=MessagePackSerializer.Deserialize<string>(request.Data!,MessagePackBinarySerializer.Options);
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject.StartsWith("Query.FuturesOptionContractQuery.GetFuturesOptionContract",StringComparison.Ordinal)||request.Subject.StartsWith("Command.FuturesOptionContractCommand.",StringComparison.Ordinal)){
            response=await optionContracts.Handle(connection,request.Subject,request.Data!,timeout.Token);
        }
        else if(request.Subject=="Query.FuturesContractQuery.GetInstrumentDefinitions.definitions"){
            var query=MessagePackSerializer.Deserialize<GetInstrumentDefinitionsQuery>(request.Data!,MessagePackBinarySerializer.Options);query.Request.Validate();
            if(query.Subject.ToString()!=request.Subject||query.Request.PageSize!=100)throw new InvalidDataException("Invalid definition request");
            var source=query.Request.Options?definitionOption:definitionFuture;
            if(definitionMode=="NoTerminal")source=source with{RawSymbol="NoTerminal",InstrumentId=9988};
            if(definitionMode=="Expired")source=source with{ExpirationUtc=definitionAt.AddDays(-1)};
            var next=query.Request.ContinuationToken is null?"opaque.definition.2":null;
            if(query.Request.ContinuationToken is not null)source=source with{InstrumentId=source.InstrumentId+1,RawSymbol=source.RawSymbol+" second"};
            if(definitionMode=="RepeatCursor"&&query.Request.ContinuationToken is not null)next=query.Request.ContinuationToken;
            var snapshot=definitionMode=="WrongSnapshot"?Guid.Parse("77ec17a2-5526-49fc-a3e0-5b2a57f04610"):source.SnapshotId;
            var items=definitionMode=="Oversized"?Enumerable.Repeat(source,101).ToArray():new[]{source};
            response=MessagePackSerializer.Serialize(new ServiceResult<InstrumentDefinitionPage>{Success=true,ErrorMessage="",Value=new(snapshot,new(2026,10,24,12,0,0,DateTimeKind.Utc),items,next)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject=="Query.NativeCompatibility.ProviderMode.none"){
            definitionMode=MessagePackSerializer.Deserialize<string>(request.Data!,MessagePackBinarySerializer.Options);
            if(definitionMode=="ChangedOriginal")futuresContracts.ChangeDescription(mappedFuture.ContractId);
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject=="Query.NativeCompatibility.OptionPageMode.none"){
            optionContracts.Mode=MessagePackSerializer.Deserialize<string>(request.Data!,MessagePackBinarySerializer.Options);
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject.StartsWith("Query.FuturesContractQuery.GetFuturesContract",StringComparison.Ordinal)||request.Subject.StartsWith("Command.FuturesContractCommand.",StringComparison.Ordinal)){
            response=await futuresContracts.Handle(connection,request.Subject,request.Data!,timeout.Token);
        }
        else if(request.Subject=="Query.NativeCompatibility.ReleaseFuturesReads.none"){
            futuresContracts.HoldReads=false;
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(DesktopFixtureMode.Enabled&&request.Subject=="Query.ApplicationQuery.GetStartupStatus.current"){
            response=MessagePackSerializer.Serialize(new ServiceResult<TomasAI.IFM.Domain.Application.Shared.ApplicationStartupStatus>{Success=true,ErrorMessage="",Value=new(){State=TomasAI.IFM.Domain.Application.Shared.ApplicationLifecycleState.Running,ValueDate=new(2026,10,26),StartedAtUtc=StrategyFixtures.At,CompletedAtUtc=StrategyFixtures.At,Summary="Synthetic desktop fixture"}},MessagePackBinarySerializer.Options);
        }
        else if(DesktopFixtureMode.Enabled&&request.Subject.StartsWith("Query.FuturesContractQuery.GetRolloverFuturesContracts.",StringComparison.Ordinal)){
            response=MessagePackSerializer.Serialize(new ServiceResult<FuturesContractV3ReadModel[]>{Success=true,ErrorMessage="",Value=[]},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject.StartsWith("Query.FuturesContractQuery.GetOnTheRunFuturesContract.",StringComparison.Ordinal)||request.Subject.StartsWith("Query.FuturesBarDataQuery.GetFuturesBarData.",StringComparison.Ordinal)||request.Subject.StartsWith("Query.MarketOutlookSnapshotQuery.",StringComparison.Ordinal)) {
            response=MarketHistoryResponder.Handle(request.Subject,request.Data!,output,outlookAvailable);
        }
        else if(DesktopFixtureMode.Enabled&&request.Subject=="Query.NativeCompatibility.DesktopOutlookAvailable.none"){
            outlookAvailable=bool.Parse(MessagePackSerializer.Deserialize<string>(request.Data!,MessagePackBinarySerializer.Options));
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject=="Query.NativeCompatibility.OutlookAvailable.none"){
            outlookAvailable=MessagePackSerializer.Deserialize<bool>(request.Data!,MessagePackBinarySerializer.Options);
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject=="Query.NativeCompatibility.CalendarHold.none") {
            holdCalendar=MessagePackSerializer.Deserialize<bool>(request.Data!,MessagePackBinarySerializer.Options);
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(holdCalendar && request.Subject.StartsWith("Query.EconomicCalendarQuery.GetEconomicCalendarCountryCodes.",StringComparison.Ordinal))continue;
        else if(DesktopFixtureMode.Enabled&&request.Subject=="Query.NativeCompatibility.DesktopEvent.none"){
            var which=MessagePackSerializer.Deserialize<string>(request.Data!,MessagePackBinarySerializer.Options);
            var file=which switch{"RecoveryFailure"=>"recovery-failure-event","Status"=>"status-notification",_=>throw new InvalidDataException("Unknown isolated desktop event")};
            var bytes=File.ReadAllBytes(Path.Combine(output,file+".wire.msgpack"));
            var subject=which=="RecoveryFailure"?MessagePackSerializer.Deserialize<MarketDataDatasetRecoveryFailedEvent>(bytes,MessagePackBinarySerializer.Options).Subject.ToString():MessagePackSerializer.Deserialize<TomasAI.IFM.Shared.StatusConsole.Events.StatusConsoleLoggedEvent>(bytes,MessagePackBinarySerializer.Options).Subject.ToString();
            await connection.PublishAsync(subject,bytes,cancellationToken:timeout.Token);
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject=="Query.NativeCompatibility.FeedScenario.none") {
            feedScenario=MessagePackSerializer.Deserialize<string>(request.Data!,MessagePackBinarySerializer.Options);
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(true)},MessagePackBinarySerializer.Options);
        }
        else if(request.Subject.StartsWith("Command.MarketDataFeedCommand.",StringComparison.Ordinal)) {
            try {
                var start=request.Subject=="Command.MarketDataFeedCommand.Start.current";
                Guid commandId;
                if(start) {
                    var command=MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands.StartMarketDataFeedCommand>(request.Data!,MessagePackBinarySerializer.Options);
                    if(command.Subject.ToString()!=request.Subject || command.RouteTo!=BoundedContextName.MarketDataFeedBoundedContext || command.EntityId.ValueDate!=default || command.ErrorCode!=5005)throw new InvalidDataException("Invalid current-session Start command");commandId=command.CommandId;
                } else {
                    var command=MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands.StopMarketDataFeedCommand>(request.Data!,MessagePackBinarySerializer.Options);
                    if(command.Subject.ToString()!=request.Subject || command.ValueDate!=date || command.EntityId.ValueDate!=date || command.RouteTo!=BoundedContextName.MarketDataFeedBoundedContext || command.ErrorCode!=5005)throw new InvalidDataException("Stop must use the API-owned ValueDate");commandId=command.CommandId;
                }
                if(feedScenario=="Reject")throw new InvalidDataException("Fixture rejected feed command");
                if(feedScenario!="Freeze")feedRuntime=feedRuntime with{IsRunning=start,ActiveValueDate=start?date:null};
                if(feedScenario=="Normal") {
                    var subject=new ActorSubject(ActorType.Event,"MarketDataFeedEvent",start?"StartedComplete":"StoppedComplete",date.ToString("yyyy-MM-dd"));
                    object terminal=start?new MarketDataFeedStartedCompleteEvent{Subject=subject,EntityId=new(date),CommandId=commandId,Id=Guid.NewGuid(),ValueDate=date,ReceivedOn=new(2026,10,26,12,30,0,DateTimeKind.Utc)}:new MarketDataFeedStoppedCompleteEvent{Subject=subject,EntityId=new(date),CommandId=commandId,Id=Guid.NewGuid(),ValueDate=date,ReceivedOn=new(2026,10,26,12,30,0,DateTimeKind.Utc)};
                    await connection.PublishAsync(subject.ToString(),MessagePackSerializer.Serialize(terminal.GetType(),terminal,MessagePackBinarySerializer.Options),cancellationToken:timeout.Token);
                }
                response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<Guid>>{Success=true,ErrorMessage="",Value=new(commandId)},MessagePackBinarySerializer.Options);
            } catch(Exception error) {
                response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<Guid>>{Success=false,ErrorCode=5005,ErrorMessage=error.Message},MessagePackBinarySerializer.Options);
            }
        }
        else if(request.Subject.StartsWith("Command.SupervisorCommand.",StringComparison.Ordinal))
        {
            try {
                var operation=request.Subject.Split('.')[2].Replace("Actor", "", StringComparison.Ordinal);
                var type=typeof(TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands.RestartSupervisorActorCommand).Assembly.GetType($"TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands.{operation}SupervisorActorCommand") ?? throw new InvalidDataException("Unknown Supervisor command");
                var command=(TomasAI.IFM.Domain.Supervisor.Shared.Operations.Commands.ISupervisorOperationCommand)MessagePackSerializer.Deserialize(type,request.Data!,MessagePackBinarySerializer.Options);
                if(command.Subject.ToString()!=request.Subject || command.Target!=new ActorThreadId(ActorType.Realtime,"IronCondorPosition","101.701.1901.1501") || command.ExpectedGeneration!=17 || command.Requester!="Fixture operator" || command.TimeoutTicks<=0 || command.TimeoutTicks>TimeSpan.FromMinutes(10).Ticks || command.OperationKind==TomasAI.IFM.Domain.Supervisor.Shared.Enums.SupervisorActorOperationKind.Stop || !command.PostEvents || command.RouteTo!=BoundedContextName.SupervisorBoundedContext)throw new InvalidDataException("Invalid Supervisor request authority/target/generation");
                if(command.Reason=="NoAck")continue;
                if(command.Reason=="Reject")throw new InvalidDataException("Supervisor fixture rejected operation");
                response=MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=true,ErrorMessage="",Value=new(command.CommandId)},MessagePackBinarySerializer.Options);
            } catch(Exception error) {
                response=MessagePackSerializer.Serialize(new ServiceResult<GuidResult>{Success=false,ErrorCode=9715,ErrorMessage=error.Message},MessagePackBinarySerializer.Options);
            }
        }
        else if(request.Subject.StartsWith("Query.EconomicCalendarQuery.",StringComparison.Ordinal) || request.Subject.StartsWith("Command.EconomicCalendarCommand.",StringComparison.Ordinal))
            response = await calendarResponder.Handle(connection, request.Subject, request.Data!, timeout.Token);
        else if(request.Subject.StartsWith("Query.YieldCurveRateQuery.",StringComparison.Ordinal))
        {
            var verb=request.Subject.Split('.')[2];
            switch(verb)
            {
                case "GetYieldCurveRateYears":
                    _=MessagePackSerializer.Deserialize<GetYieldCurveRateYearsQuery>(request.Data!,MessagePackBinarySerializer.Options);
                    response=MessagePackSerializer.Serialize(new ServiceResult<YieldCurveRateYearsReadModel>{Success=true,ErrorMessage="",Value=new(){Years=yieldRows.Keys.Select(d=>d.Year).Distinct().ToArray()}},MessagePackBinarySerializer.Options);break;
                case "GetYieldCurveRates":
                    var range=MessagePackSerializer.Deserialize<GetYieldCurveRatesQuery>(request.Data!,MessagePackBinarySerializer.Options);
                    if(range.Subject.ToString()!=request.Subject)throw new InvalidDataException("Curve range subject mismatch");
                    response=MessagePackSerializer.Serialize(new ServiceResult<YieldCurveRateReadModel[]>{Success=true,ErrorMessage="",Value=yieldRows.Where(p=>p.Key>=range.StartDate&&p.Key<=range.EndDate).Select(p=>p.Value).ToArray()},MessagePackBinarySerializer.Options);break;
                case "GetYieldCurveRateExists":
                    var exists=MessagePackSerializer.Deserialize<GetYieldCurveRateExistsQuery>(request.Data!,MessagePackBinarySerializer.Options);
                    response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<bool>>{Success=true,ErrorMessage="",Value=new(yieldRows.ContainsKey(exists.ValueDate))},MessagePackBinarySerializer.Options);break;
            }
        }
        else if(request.Subject.StartsWith("Command.YieldCurveRateCommand.",StringComparison.Ordinal))
        {
            var verb=request.Subject.Split('.')[2];Guid commandId;YieldCurveRateEntityId entity;DateOnly day;object terminal;
            var now=new DateTime(2026,10,26,12,30,0,DateTimeKind.Utc);
            switch(verb)
            {
                case "Add":
                    var add=MessagePackSerializer.Deserialize<AddYieldCurveRateCommand>(request.Data!,MessagePackBinarySerializer.Options);commandId=add.CommandId;entity=add.EntityId;day=add.YieldCurveRate.ValueDate;
                    if(add.Overwrite||add.RouteTo!=BoundedContextName.YieldCurveRateBoundedContext||add.ErrorCode!=AddYieldCurveRateCommand.ErrorId||entity.Year!=day.Year||add.Subject.ToString()!=request.Subject)throw new InvalidDataException("Invalid curve add envelope");
                    yieldRows.Add(day,add.YieldCurveRate);
                    terminal=new YieldCurveRateAddedCompleteEvent{CommandId=commandId,EntityId=entity,Id=Guid.NewGuid(),ReceivedOn=now,YieldCurveRate=add.YieldCurveRate,CreatedOn=now,CreatedBy="Fixture",Subject=new(ActorType.Event,"YieldCurveRateEvent","AddedComplete",entity.Format())};break;
                case "Change":
                    var change=MessagePackSerializer.Deserialize<ChangeYieldCurveRateCommand>(request.Data!,MessagePackBinarySerializer.Options);commandId=change.CommandId;entity=change.EntityId;day=change.YieldCurveRate.ValueDate;
                    if(!change.Overwrite||change.RouteTo!=BoundedContextName.YieldCurveRateBoundedContext||change.ErrorCode!=ChangeYieldCurveRateCommand.ErrorId||entity.Year!=day.Year||change.Subject.ToString()!=request.Subject)throw new InvalidDataException("Invalid curve change envelope");
                    yieldRows[day]=change.YieldCurveRate;
                    terminal=new YieldCurveRateChangedCompleteEvent{CommandId=commandId,EntityId=entity,Id=Guid.NewGuid(),ReceivedOn=now,YieldCurveRate=change.YieldCurveRate,UpdatedOn=now,UpdatedBy="Fixture",Subject=new(ActorType.Event,"YieldCurveRateEvent","ChangedComplete",entity.Format())};break;
                case "Remove":
                    var remove=MessagePackSerializer.Deserialize<RemoveYieldCurveRateCommand>(request.Data!,MessagePackBinarySerializer.Options);commandId=remove.CommandId;entity=remove.EntityId;day=remove.ValueDate;
                    if(!remove.Overwrite||remove.RouteTo!=BoundedContextName.YieldCurveRateBoundedContext||remove.ErrorCode!=RemoveYieldCurveRateCommand.ErrorId||entity.Year!=day.Year||remove.Subject.ToString()!=request.Subject)throw new InvalidDataException("Invalid curve remove envelope");
                    yieldRows.Remove(day);
                    terminal=new YieldCurveRateRemovedCompleteEvent{CommandId=commandId,EntityId=entity,Id=Guid.NewGuid(),ReceivedOn=now,ValueDate=day,DeletedOn=now,DeletedBy="Fixture",Subject=new(ActorType.Event,"YieldCurveRateEvent","RemovedComplete",entity.Format())};break;
                case "Import":
                    var import=MessagePackSerializer.Deserialize<ImportYieldCurveRatesCommand>(request.Data!,MessagePackBinarySerializer.Options);commandId=import.CommandId;entity=import.EntityId;day=DateOnly.FromDateTime(import.ImportDate);
                    if(import.DuplicatePolicy!=ImportDuplicatePolicy.Overwrite||import.RouteTo!=BoundedContextName.YieldCurveRateBoundedContext||import.ErrorCode!=ImportYieldCurveRatesCommand.ErrorId||entity.Year!=day.Year||import.Subject.ToString()!=request.Subject)throw new InvalidDataException("Invalid curve import envelope");
                    var curve=new YieldCurveRateReadModel(day,1.1,1.2,1.3,1.4,1.5,1.6,1.7,1.8,1.9,2.0,2.1,2.2);yieldRows[day]=curve;
                    terminal=new YieldCurveRatesImportedCompleteEvent{CommandId=commandId,EntityId=entity,Id=Guid.NewGuid(),ReceivedOn=now,ImportDate=import.ImportDate,YieldCurveRates=[curve],ImportedOn=now,ImportedBy="Fixture",Subject=new(ActorType.Event,"YieldCurveRateEvent","ImportedComplete",entity.Format())};break;
                default:throw new InvalidDataException("Unexpected curve command");
            }
            var eventSubject=((TomasAI.IFM.Shared.EventSourcing.IEvent)terminal).Subject.ToString();
            await connection.PublishAsync(eventSubject,MessagePackSerializer.Serialize(terminal.GetType(),terminal,MessagePackBinarySerializer.Options),cancellationToken:timeout.Token);
            response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<Guid>>{Success=true,ErrorMessage="",Value=new(commandId)},MessagePackBinarySerializer.Options);
        }
        else if (request.Subject == "Query.ReferenceQuery.StrategyCatalog.none")
        {
            var query = MessagePackSerializer.Deserialize<StrategyCatalogQuery>(request.Data!, MessagePackBinarySerializer.Options);
            if (query.Subject.ToString() != request.Subject) throw new InvalidDataException("Catalog subject/payload mismatch");
            var parameters = StrategyCatalogJson.Read<CatalogQueryRequest>(query.RequestJson);
            var json = parameters.Operation == CatalogQueryOperation.DeploymentChoices
                ? StrategyCatalogJson.Write(new{Items=portfolios.HasDeployments?new[]{PortfolioResponder.Deployment}:[],NextCode=(string?)null})
                : parameters.Operation == CatalogQueryOperation.List
                ? StrategyCatalogJson.Write(catalog.Values.Where(v => v.Definition.Key.Kind == parameters.Kind &&
                    (parameters.AfterCode == null || string.CompareOrdinal(v.Definition.Code, parameters.AfterCode) > 0))
                    .OrderBy(v => v.Definition.Code, StringComparer.Ordinal).Take(parameters.Limit)
                    .Select(v => new StrategyCatalogSummary(v.Definition.Key,v.Definition.Code,v.Definition.Name,v.Status,v.ContentHash)).ToArray())
                : StrategyCatalogJson.Write(catalog[parameters.Key!]);
            response = MessagePackSerializer.Serialize(new ServiceResult<string> { Success = true, ErrorMessage = "", Value = json }, MessagePackBinarySerializer.Options);
        }
        else if(request.Subject=="Query.ReferenceQuery.GetLookupDefinitions.none"){
            var query=MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.Reference.Shared.Lookups.GetLookupDefinitionsQuery>(request.Data!,MessagePackBinarySerializer.Options);
            if(query.Subject.ToString()!=request.Subject||query.EntityId.Format()!="none")throw new InvalidDataException("Invalid Fund lookup route");
            response=MessagePackSerializer.Serialize(new ServiceResult<TomasAI.IFM.Domain.Reference.Shared.Lookups.LookupDefinitionReadModel[]>{Success=true,ErrorMessage="",Value=PortfolioResponder.LookupChoices(query.GroupName)},MessagePackBinarySerializer.Options);
        }
        else if (request.Subject == "Query.ReferenceQuery.GetTradeStrategySymbols.none")
        {
            var query = MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.Reference.Shared.Queries.GetTradeStrategySymbolsQuery>(request.Data!, MessagePackBinarySerializer.Options);
            if (query.Subject.ToString() != request.Subject || query.Family is not (TradeStrategyFamilyType.Futures or TradeStrategyFamilyType.FuturesOption)) throw new InvalidDataException("Invalid product query");
            response = MessagePackSerializer.Serialize(new ServiceResult<TradeStrategySymbolReadModel[]> { Success = true, ErrorMessage = "", Value = [new() {Id=1, Symbol="ES", Currency="USD", Exchange="CME", Description="E-mini S&P 500"}] }, MessagePackBinarySerializer.Options);
        }
        else if (request.Subject == "Command.TradeStrategyFamilyCommand.Catalog.none")
        {
            try
            {
                var command = MessagePackSerializer.Deserialize<StrategyCatalogCommand>(request.Data!, MessagePackBinarySerializer.Options);
                var parameters = StrategyCatalogJson.Read<CatalogCommandRequest>(command.RequestJson);
                if (command.Subject.ToString() != request.Subject || parameters.OperationId != command.CommandId || command.ErrorCode != StrategyCatalogCommand.ErrorId || command.RouteTo != BoundedContextName.TradeStrategyFamilyBoundedContext) throw new InvalidDataException("Invalid catalog command envelope");
                if (catalogOperations.TryGetValue(command.CommandId, out var prior) && prior != command.RequestJson) throw new InvalidDataException("OperationId reused for another request");
                if (!catalogOperations.ContainsKey(command.CommandId))
                {
                    switch (parameters.Operation)
                    {
                        case CatalogCommandOperation.SaveDraft:
                            var definition = parameters.Definition ?? throw new InvalidDataException("Missing definition");
                            if (definition.Key.Version != parameters.ExpectedPreviousVersion + 1 || string.IsNullOrWhiteSpace(definition.Code) || string.IsNullOrWhiteSpace(definition.Name)) throw new InvalidDataException("Invalid draft/version");
                            if (catalog.ContainsKey(definition.Key)) throw new InvalidDataException("Immutable catalog version already exists");
                            catalog.Add(definition.Key, new(definition, FinancialCanonicalHash.Compute(definition), CatalogLifecycleStatus.Draft, new(2026,10,26,12,30,0,DateTimeKind.Utc), "Fixture", null,null,null,null));
                            break;
                        case CatalogCommandOperation.Publish:
                        case CatalogCommandOperation.Retire:
                            var key = parameters.Key ?? throw new InvalidDataException("Missing catalog key");
                            var stored = catalog[key];
                            if (parameters.ExpectedHash != stored.ContentHash || parameters.EffectiveUtc?.Kind != DateTimeKind.Utc || stored.Status != (parameters.Operation == CatalogCommandOperation.Publish ? CatalogLifecycleStatus.Draft : CatalogLifecycleStatus.Published)) throw new InvalidDataException("Invalid catalog lifecycle authority");
                            catalog[key] = stored with {Status = parameters.Operation == CatalogCommandOperation.Publish ? CatalogLifecycleStatus.Published : CatalogLifecycleStatus.Retired};
                            break;
                        default: throw new InvalidDataException("Unsupported catalog command");
                    }
                    catalogOperations.Add(command.CommandId, command.RequestJson);
                }
                response = MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<Guid>> { Success = true, ErrorMessage = "", Value = new(command.CommandId) }, MessagePackBinarySerializer.Options);
            }
            catch (Exception error)
            {
                response = MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<Guid>> { Success = false, ErrorCode = 8064, ErrorMessage = error.Message }, MessagePackBinarySerializer.Options);
            }
        }
        else if (request.Subject == "Query.MarketDataQuery.GetValueDate.valueDate")
        {
            var query = MessagePackSerializer.Deserialize<GetValueDateQuery>(request.Data!, MessagePackBinarySerializer.Options);
            if (query.Subject.ToString() != request.Subject) throw new InvalidDataException("Subject/payload mismatch");
            response = File.ReadAllBytes(Path.Combine(output, "value-date-result.wire.msgpack"));
        }
        else if (request.Subject == "Query.MarketDataQuery.GetMarketSession.marketSession")
        {
            var query = MessagePackSerializer.Deserialize<GetMarketSessionQuery>(request.Data!, MessagePackBinarySerializer.Options);
            if (query.Subject.ToString() != request.Subject) throw new InvalidDataException("Subject/payload mismatch");
            response = File.ReadAllBytes(Path.Combine(output, "market-session-result.wire.msgpack"));
            if(DesktopFixtureMode.OperationalClock){
                var current=MessagePackSerializer.Deserialize<ServiceResult<MarketSessionReadModel>>(response,MessagePackBinarySerializer.Options);
                response=MessagePackSerializer.Serialize(new ServiceResult<MarketSessionReadModel>{Success=true,ErrorMessage="",Value=current.Value! with{AsOfUtc=DateTime.UtcNow}},MessagePackBinarySerializer.Options);
            }
        }
        else if(DesktopFixtureMode.Enabled&&request.Subject=="Query.MarketDataFeedQuery.GetDatabentoReadiness.databento-readiness"){
            response=MessagePackSerializer.Serialize(new ServiceResult<DatabentoReadinessReadModel>{Success=true,ErrorMessage="",Value=new(){State=feedRuntime.IsRunning?"Healthy":"Stopped",DisplayHealth=feedRuntime.IsRunning?"Green":"Red",CoreReady=feedRuntime.IsRunning,ValueDate=date,ChangedOnUtc=StrategyFixtures.At,Reason="Synthetic desktop fixture"}},MessagePackBinarySerializer.Options);
        }
        else if (request.Subject == "Query.MarketDataFeedQuery.GetRuntimeStatus.runtime-status")
        {
            var query = MessagePackSerializer.Deserialize<GetMarketDataFeedRuntimeStatusQuery>(request.Data!, MessagePackBinarySerializer.Options);
            if (query.Subject.ToString() != request.Subject) throw new InvalidDataException("Subject/payload mismatch");
            response = MessagePackSerializer.Serialize(new ServiceResult<MarketDataFeedRuntimeStatusReadModel>{Success=true,ErrorMessage="",Value=feedRuntime},MessagePackBinarySerializer.Options);
        }
        else if(DesktopFixtureMode.Enabled&&request.Subject.StartsWith("Query.LookupTypeQuery.GetLookupType.",StringComparison.Ordinal)){
            var query=MessagePackSerializer.Deserialize<GetLookupTypeQuery>(request.Data!,MessagePackBinarySerializer.Options);
            if(query.Subject.ToString()!=request.Subject||request.Subject!="Query.LookupTypeQuery.GetLookupType."+query.LookupTypeName)throw new InvalidDataException("Specific lookup route differs from its typed name");
            response=MessagePackSerializer.Serialize(new ServiceResult<LookupTypeCollection>{Success=true,ErrorMessage="",Value=new(lookupRows.Where(r=>r.LookupTypeName==query.LookupTypeName).ToList())},MessagePackBinarySerializer.Options);
        }
        else if(DesktopFixtureMode.Enabled&&request.Subject=="Query.LookupTypeQuery.GetLookupTypeNames.all"){
            response=MessagePackSerializer.Serialize(new ServiceResult<string[]>{Success=true,ErrorMessage="",Value=lookupRows.Select(r=>r.LookupTypeName).Distinct(StringComparer.Ordinal).OrderBy(n=>n,StringComparer.Ordinal).ToArray()},MessagePackBinarySerializer.Options);
        }
        else if (request.Subject == "Query.LookupTypeQuery.GetLookupTypes.all")
        {
            var query = MessagePackSerializer.Deserialize<GetLookupTypesQuery>(request.Data!, MessagePackBinarySerializer.Options);
            if (query.Subject.ToString() != request.Subject || query.EntityId.Format() != "none") throw new InvalidDataException("Subject/entity mismatch");
            response = MessagePackSerializer.Serialize(new ServiceResult<LookupTypeCollection> {Success=true,ErrorMessage="",Value=new(lookupRows.ToList())},MessagePackBinarySerializer.Options);
        }
        else if (request.Subject.StartsWith("Command.LookupTypeCommand.",StringComparison.Ordinal))
        {
            try
            {
                var verb = request.Subject.Split('.')[2];
                Guid commandId; LookupTypeId entity; LookupTypeReadModel? model = null;
                switch(verb)
                {
                    case "Add":
                        var add = MessagePackSerializer.Deserialize<AddLookupTypeCommand>(request.Data!,MessagePackBinarySerializer.Options);
                        if(add.Subject.ToString()!=request.Subject || add.RouteTo!=BoundedContextName.LookupTypeBoundedContext || add.ErrorCode!=8001)throw new InvalidDataException("Invalid add envelope");
                        commandId=add.CommandId;entity=add.EntityId;model=add.LookupType;
                        if(lookupRows.Any(r=>r.Id==entity))throw new InvalidDataException("Lookup already exists");
                        break;
                    case "Change":
                        var change = MessagePackSerializer.Deserialize<ChangeLookupTypeCommand>(request.Data!,MessagePackBinarySerializer.Options);
                        if(change.Subject.ToString()!=request.Subject || change.RouteTo!=BoundedContextName.LookupTypeBoundedContext || change.ErrorCode!=8002 || change.Overwrite)throw new InvalidDataException("Invalid change envelope");
                        commandId=change.CommandId;entity=change.EntityId;model=change.LookupType;
                        if(!lookupRows.Any(r=>r.Id==entity))throw new InvalidDataException("Lookup not found");
                        break;
                    case "Remove":
                        var remove = MessagePackSerializer.Deserialize<RemoveLookupTypeCommand>(request.Data!,MessagePackBinarySerializer.Options);
                        if(remove.Subject.ToString()!=request.Subject || remove.RouteTo!=BoundedContextName.LookupTypeBoundedContext || remove.ErrorCode!=8002 || remove.Overwrite)throw new InvalidDataException("Invalid remove envelope");
                        commandId=remove.CommandId;entity=remove.EntityId;
                        if(!lookupRows.Any(r=>r.Id==entity))throw new InvalidDataException("Lookup not found");
                        break;
                    default:throw new InvalidDataException("Unexpected lookup command");
                }
                if(model?.ShortCode=="REJECT")throw new InvalidDataException("Fixture rejected lookup");
                lookupRows.RemoveAll(r=>r.Id==entity);
                if(model!=null)lookupRows.Add(model);
                var terminalVerb = verb switch {"Add"=>"AddedComplete","Change"=>"ChangedComplete",_=>"RemovedComplete"};
                var subject = new ActorSubject(ActorType.Event,"LookupTypeEvent",terminalVerb,entity.Format());
                var now = new DateTime(2026,10,26,12,30,0,DateTimeKind.Utc);
                object terminal = verb switch {
                    "Add"=>new LookupTypeAddedCompleteEvent {Subject=subject,EntityId=entity,CommandId=commandId,Id=Guid.NewGuid(),ReceivedOn=now,LookupType=model!,AddedOn=now,AddedBy="Fixture"},
                    "Change"=>new LookupTypeChangedCompleteEvent {Subject=subject,EntityId=entity,CommandId=commandId,Id=Guid.NewGuid(),ReceivedOn=now,LookupTypeId=entity,LookupType=model!,ChangedOn=now,ChangedBy="Fixture"},
                    _=>new LookupTypeRemovedCompleteEvent {Subject=subject,EntityId=entity,CommandId=commandId,Id=Guid.NewGuid(),ReceivedOn=now,LookupTypeId=entity,RemovedOn=now,RemovedBy="Fixture"}
                };
                if(model?.Description!="NoTerminal")
                {
                    // Publish before the request acknowledgement, exercising the event-before-reply race.
                    var eventBytes = MessagePackSerializer.Serialize(terminal.GetType(),terminal,MessagePackBinarySerializer.Options);
                    await connection.PublishAsync(subject.ToString(),eventBytes,cancellationToken:timeout.Token);
                }
                response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<Guid>> {Success=true,ErrorMessage="",Value=new(commandId)},MessagePackBinarySerializer.Options);
            }
            catch(Exception error)
            {
                response=MessagePackSerializer.Serialize(new ServiceResult<ScalarReadModel<Guid>> {Success=false,ErrorCode=8001,ErrorMessage=error.Message},MessagePackBinarySerializer.Options);
            }
        }
        else if (request.Subject == "Query.NativeCompatibility.Echo.none")
        {
            var values = MessagePackSerializer.Deserialize<string[]>(request.Data!, MessagePackBinarySerializer.Options);
            if (values.Length != 20000) throw new InvalidDataException("Incomplete Rust compressed input");
            response = MessagePackSerializer.Serialize(values, MessagePackBinarySerializer.Options);
        }
        else if (request.Subject == "Query.NativeCompatibility.Error.none") response = File.ReadAllBytes(Path.Combine(output, "error-result.wire.msgpack"));
        else if(DesktopFixtureMode.Enabled){
            Console.Error.WriteLine("UNHANDLED desktop fixture: "+request.Subject);
            response=MessagePackSerializer.Serialize(new ServiceResult<object>{Success=false,ErrorCode=59999,ErrorMessage="No isolated desktop fixture for "+request.Subject},MessagePackBinarySerializer.Options);
        }
        if (response != null) await connection.PublishAsync(request.ReplyTo, response, cancellationToken: timeout.Token);
    }
}
