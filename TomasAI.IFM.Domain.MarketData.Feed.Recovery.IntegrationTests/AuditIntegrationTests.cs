using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Shared.Identity;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.EventProjector;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NATS.Client.Core;
using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventModelActor;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Recovery;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Function.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Recovery.Function.State;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
namespace TomasAI.IFM.Domain.MarketData.Feed.Recovery.IntegrationTests;
/// <summary>Real PostgreSQL and NATS, with actual actor/function/repository implementations. Ports are isolated by the runner.</summary>
public sealed class AuditIntegrationTests
{
    static readonly MessagePackSerializerOptions Wire=MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
    static string Connection
    {
        get
        {
            var value=Environment.GetEnvironmentVariable("IFM_RECOVERY_AUDIT_TEST_CONNECTION") ?? throw new InvalidOperationException("Run scripts/Test-MarketDataRecoveryAudit.ps1 to provision an isolated PostgreSQL/NATS fixture.");
            if (!new NpgsqlConnectionStringBuilder(value).Database!.StartsWith("ifm_recovery_audit_",StringComparison.Ordinal)) throw new InvalidOperationException("A dedicated recovery audit test database is mandatory.");
            return value;
        }
    }
    static RecoveryRecord Start() { var at=DateTimeOffset.UtcNow;return new() {Version=1,RecordId=Guid.NewGuid(),OperationId=Guid.NewGuid(),Sequence=1,Kind="Started",RuntimeId=Guid.NewGuid().ToString(),Role="Options",Dataset="GLBX.MDP3",ValueDate=new(2026,10,29),OccurredAtUtc=at,StartedAtUtc=at,RecoveryDeadlineUtc=at.AddMinutes(2),MissingProofs=["Transport","Pricing"]}; }
    static RecordMarketDataFeedRecoveryCommand Request(RecoveryRecord record)
    {
        var json=JsonSerializer.Serialize(record);return new() {CommandId=Guid.NewGuid(),AttemptId=Guid.NewGuid(),EntityId=new(record.RecordId),
            Subject=new(ActorType.Function,RecordMarketDataFeedRecoveryCommand.Actor,"Record",record.RecordId.ToString()),Payload=json,Digest=RecoveryRecord.Digest(json),AttemptStartedAtUtc=DateTime.UtcNow,RequestDeadlineUtc=DateTime.UtcNow.AddMinutes(2)};
    }
    [Fact]
    public async Task Actor_function_reconciles_607_duplicates_snapshot_tail_and_crash_between_commits()
    {
        await using var fixture=await Fixture.Create();var record=Start();
        var request=Request(record);
        // Aggregate commits, function response is lost before its own completed event.
        var append=fixture.Append(request);var appended=await fixture.Command(append);Assert.True(appended.Success,appended.ErrorMessage);
        Assert.Equal(1,await fixture.Count(append.StreamId));
        var first=await fixture.Record(request);Assert.True(first.IsCompleted,first.Failed?.ErrorData);
        for(var i=0;i<607;i++) {var reply=await fixture.Record(Request(record));Assert.True(reply.IsCompleted,reply.Failed?.ErrorMessage);}
        Assert.Equal(1,await fixture.Count(append.StreamId));Assert.Equal(1,await fixture.Count(request.StreamId));
        var completed=record with {RecordId=Guid.NewGuid(),Sequence=2,Kind="Completed",CompletionScope="TransportOnly",ProviderActivityVerified=true};
        Assert.True((await fixture.Record(Request(completed))).IsCompleted);
        Assert.Equal(2,await fixture.Count(append.StreamId));
        var restored=await fixture.Repository.LoadStateAsync(append);Assert.Equal(2,restored.Latest!.Sequence);Assert.Equal(record.RecordId,restored.Started!.RecordId);
        var conflict=await fixture.Record(Request(record with { LatestError="tampered" }));Assert.True(conflict.IsFailed);
        Assert.Equal(2,await fixture.Count(append.StreamId));
        // Target database failure cannot invalidate the already committed Function reply.
        var unavailable=new NpgsqlConnectionStringBuilder(Connection) {Host="127.0.0.1",Port=1};
        await Assert.ThrowsAsync<NpgsqlException>(()=>MarketDataRecoveryLog.ProjectAsync(unavailable.ConnectionString,fixture.Facts.First()));
        foreach(var fact in fixture.Facts) await MarketDataRecoveryLog.ProjectAsync(Connection,fact);
        var rows=await MarketDataRecoveryLog.QueryAsync(Connection,record.ValueDate,record.Dataset,record.OperationId);
        Assert.Equal(2,rows.Count);
        // Rebuild from replayed immutable facts produces exactly the same rows.
        await fixture.Sql("DELETE FROM market_data_service.marketdata_recovery_log WHERE operation_id='"+record.OperationId+"'");
        foreach(var fact in fixture.Facts) await MarketDataRecoveryLog.ProjectAsync(Connection,fact);
        Assert.Equal(2,(await MarketDataRecoveryLog.QueryAsync(Connection,record.ValueDate,record.Dataset,record.OperationId)).Count);
    }
    [Theory]
    [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)]
    public async Task Native_outbox_delivers_via_real_Nats_function_then_projects_to_Postgres(bool loseFirstReply,bool finalFailure)
    {
        await using var fixture=await Fixture.Create();
        var url=Environment.GetEnvironmentVariable("IFM_RECOVERY_AUDIT_TEST_NATS") ?? throw new InvalidOperationException("Isolated NATS URL required.");
        var binary=Environment.GetEnvironmentVariable("IFM_RECOVERY_AUDIT_NATIVE_TEST_EXE") ?? throw new InvalidOperationException("Native qualification binary required.");
        var record=Start() with {OldOwner="old-worker/old-generation",OriginalError="Provider disconnected"};
        var timeline=new List<RecoveryRecord> {record};
        if(loseFirstReply || finalFailure) {
            timeline.Add(record with {RecordId=Guid.NewGuid(),Sequence=2,Kind="ResetStarted",ResetDeadlineUtc=record.RecoveryDeadlineUtc.AddMinutes(2)});
            if(finalFailure) timeline.Add(record with {RecordId=Guid.NewGuid(),Sequence=3,Kind="Failed",LatestError="Replacement connection could not be verified"});
            else {
                var replacement=record with {NewOwner="new-worker/new-generation",OldResourcesRetired=true};
                timeline.Add(replacement with {RecordId=Guid.NewGuid(),Sequence=3,Kind="ResetCompleted"});
                timeline.Add(replacement with {RecordId=Guid.NewGuid(),Sequence=4,Kind="ConnectionRestored",ProviderActivityVerified=true});
                timeline.Add(replacement with {RecordId=Guid.NewGuid(),Sequence=5,Kind="Completed",ProviderActivityVerified=true,MonitoringReady=true,MissingProofs=[],CompletionScope="MonitoringReady"});
            }
        } else timeline.Add(record with {RecordId=Guid.NewGuid(),Sequence=2,Kind="Completed",ProviderActivityVerified=true});
        var input=Path.GetTempFileName();await File.WriteAllTextAsync(input,JsonSerializer.Serialize(timeline.Select(item=>JsonSerializer.Serialize(item))));
        using var stop=new CancellationTokenSource(TimeSpan.FromMinutes(3));
        await using var connection=new NatsConnection(new NatsOpts { Url=url });await connection.ConnectAsync();
        await using var subscription=await connection.SubscribeCoreAsync<byte[]>("Function.MarketDataFeedRecoveryRecordFunction.Record.*",cancellationToken:stop.Token);
        var attempts=0;
        var serving=Task.Run(async ()=> {await foreach(var message in subscription.Msgs.ReadAllAsync(stop.Token)) {
            var request=MessagePackSerializer.Deserialize<RecordMarketDataFeedRecoveryCommand>(message.Data!,Wire);
            var result=await fixture.Record(request);
            Assert.True(result.IsCompleted,result.Failed?.ErrorData+"; "+result.Failed?.ErrorMessage);
            if(loseFirstReply && Interlocked.Increment(ref attempts)==1) continue;
            await message.ReplyAsync(MessagePackSerializer.Serialize(new ServiceResult<FunctionResult<MarketDataFeedRecoveryRecordStoredEvent,MarketDataFeedRecoveryRecordStorageFailedEvent>>(result),Wire),cancellationToken:stop.Token);
        }},stop.Token);
        await connection.PingAsync(stop.Token);
        try
        {
            var start=new ProcessStartInfo(binary,"--exact audit::tests::real_nats_function_delivery --ignored --nocapture") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            start.Environment["IFM_RECOVERY_AUDIT_TEST_NATS"]=url;start.Environment["IFM_RECOVERY_AUDIT_TEST_RECORDS"]=input;
            start.Environment["IFM_RECOVERY_AUDIT_TEST_TICKS"]=DateTimeOffset.UtcNow.Subtract(DateTimeOffset.UnixEpoch).Ticks.ToString();
            using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
            var exit=process.WaitForExitAsync(stop.Token);
            try {
                var finished=await Task.WhenAny(exit,serving);
                if(finished==serving) await serving; // Surface responder exceptions immediately.
                await exit;Assert.True(process.ExitCode==0,(await output)+(await error));
            }
            finally {if(!process.HasExited){process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}}
            foreach(var fact in fixture.Facts) await MarketDataRecoveryLog.ProjectAsync(Connection,fact);
            var rows=await MarketDataRecoveryLog.QueryAsync(Connection,record.ValueDate,record.Dataset,record.OperationId);Assert.Equal(timeline.Count,rows.Count);
            Assert.Equal(timeline.Select(item=>JsonSerializer.Serialize(item)),rows);
            var evidence=Environment.GetEnvironmentVariable("IFM_RECOVERY_AUDIT_EVIDENCE")??Path.GetTempPath();
            await File.WriteAllTextAsync(Path.Combine(evidence,$"recovery-native-timeline-{loseFirstReply}-{finalFailure}.json"),"["+string.Join(",",rows)+"]");
        }
        finally {stop.Cancel();try{await serving;}catch(OperationCanceledException){}File.Delete(input);}
    }
    [Fact]
    public async Task Durable_commit_and_projection_logs_reach_an_actual_OTLP_HTTP_receiver()
    {
        var record=Start();var received=new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder=Microsoft.AspNetCore.Builder.WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();builder.WebHost.ConfigureKestrel(options=>options.Listen(System.Net.IPAddress.Loopback,0));
        await using var app=builder.Build();
        app.MapPost("/v1/logs",async (Microsoft.AspNetCore.Http.HttpContext context)=> {
            using var bytes=new MemoryStream();await context.Request.Body.CopyToAsync(bytes);
            var body=bytes.ToArray();if(Encoding.UTF8.GetString(body).Contains(record.RecordId.ToString(),StringComparison.Ordinal) && Encoding.UTF8.GetString(body).Contains("Recovery projection completed",StringComparison.Ordinal))received.TrySetResult(body);
            context.Response.ContentType="application/x-protobuf";context.Response.StatusCode=200;
        });
        await app.StartAsync();
        var configuration=new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Telemetry:Logs:OtlpEndpoint"]=app.Urls.Single()+"/v1/logs",["Telemetry:Logs:OtlpProtocol"]="http/protobuf" }).Build();
        var file=Path.GetTempFileName();
        using var log=new Serilog.LoggerConfiguration().MinimumLevel.Debug().WriteTo.File(file,outputTemplate:"{Timestamp:o} {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.Sink(new TomasAI.IFM.Framework.Telemetry.Logging.OtlpStructuredLogSink(configuration,"recovery-audit-qualification")).CreateLogger();
        using var logging=new Serilog.Extensions.Logging.SerilogLoggerFactory(log,dispose:false);
        await using var fixture=await Fixture.Create(logging);
        var stored=await fixture.Record(Request(record));Assert.True(stored.IsCompleted,stored.Failed?.ErrorData);
        var settings=new DbConnectionSettings().Add(MarketDataServiceDbContext.MarketDataServiceDbConnection,Connection,"System.Data.Postgres");
        var databases=Substitute.For<IDbContextFactory>();
        var store=new MarketDataServiceDbContext(settings,databases,Substitute.For<ISequenceIdDbContext>(),NullLogger<DbProvider>.Instance);
        databases.MarketDataServiceDb.Returns(store);
        var projector=new MarketDataFeedRecoveryEventProjector(Substitute.For<IDurableReplayQueue>(),fixture.Source,Substitute.For<IBlackboardService>(),databases,logging.CreateLogger<MarketDataFeedRecoveryEventProjector>());
        foreach(var fact in fixture.Facts)
        {
            var descriptor=projector.ProjectionDescriptors.Single(d=>d.SourceEventType==fact.GetType());
            Assert.True(descriptor.UseDurableReplay);Assert.NotNull(descriptor.RebuildReadModelAsync);
            var execution=new ProjectionExecutionContext(projector.ProjectorName,fact.EventId,1,new(projector.ProjectorName,fact.EventId,EventProjectorEffectKind.TargetProjection),Guid.NewGuid(),EventProjectionIdempotencyStrategy.NaturalKeyMutation,CancellationToken.None);
            await descriptor.ApplyAsync(fact,execution);
        }
        var bytes=await received.Task.WaitAsync(TimeSpan.FromSeconds(30));var body=Encoding.UTF8.GetString(bytes);
        Assert.Contains(record.RecordId.ToString(),body);Assert.Contains(record.OperationId.ToString(),body);Assert.Contains("OccurredAtUtc",body);Assert.Contains("Transition",body);
        using var reader=new StreamReader(new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite));
        var durable=await reader.ReadToEndAsync();Assert.Contains(record.RecordId.ToString(),durable);Assert.Contains(record.OperationId.ToString(),durable);Assert.Contains("Recovery fact committed",durable);
        var evidence=Path.Combine(Environment.GetEnvironmentVariable("IFM_RECOVERY_AUDIT_EVIDENCE")??Path.GetTempPath(),"recovery-otel-received.pb");
        await File.WriteAllBytesAsync(evidence,bytes);
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(evidence)!,"recovery-server.log"),durable);
        await app.StopAsync();
    }
    [Fact]
    public async Task Projection_creates_its_additive_table_when_full_deployment_schema_initialization_was_not_run()
    {
        await using var fixture=await Fixture.Create();var record=Start();Assert.True((await fixture.Record(Request(record))).IsCompleted);
        // A new configured target identity exercises cold initialization against the same dedicated test database.
        var cold=new NpgsqlConnectionStringBuilder(Connection) {ApplicationName="recovery-audit-cold-schema"};
        await fixture.Sql("DROP TABLE market_data_service.marketdata_recovery_log");
        await MarketDataRecoveryLog.ProjectAsync(cold.ConnectionString,fixture.Facts.Single());
        Assert.Single(await MarketDataRecoveryLog.QueryAsync(cold.ConnectionString,record.ValueDate,record.Dataset,record.OperationId));
    }
    [Fact]
    public async Task Real_JetStream_replay_survives_projector_restart_and_updates_the_durable_checkpoint()
    {
        await using var fixture=await Fixture.Create();var record=Start();Assert.True((await fixture.Record(Request(record))).IsCompleted);
        var fact=fixture.Facts.Single();var url=Environment.GetEnvironmentVariable("IFM_RECOVERY_AUDIT_TEST_NATS")!;
        var options=new NatsJetStreamConsumerOptions {Url=url};
        const string name=nameof(MarketDataFeedRecoveryEventProjector);
        using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await using(var producer=new NatsJSDurableReplayQueue(options)) {
            await producer.PrepareAsync(name,TimeSpan.FromMilliseconds(100),deadline.Token);
            await producer.EnqueueAsync(name,fact,deadline.Token);
            await producer.EnqueueAsync(name,fact,deadline.Token);
        }
        var settings=new DbConnectionSettings().Add(MarketDataServiceDbContext.MarketDataServiceDbConnection,Connection,"System.Data.Postgres");
        var databases=Substitute.For<IDbContextFactory>();databases.MarketDataServiceDb.Returns(new MarketDataServiceDbContext(settings,databases,Substitute.For<ISequenceIdDbContext>(),NullLogger<DbProvider>.Instance));
        var context=Substitute.For<ICommandActorContext>();context.ActorId.Returns(new ActorMailboxId(ActorType.Command,MarketDataFeedRecoveryCommandActor.ActorName));
        // A transient allowlist must not turn this mandatory audit projector into an in-memory queue.
        var reliability=new EventProjectorReliabilityOptions {InitialReplayDelay=TimeSpan.FromMilliseconds(100),DurableProjectorAllowlist=[]};
        await using(var queue=new NatsJSDurableReplayQueue(options)) {
            var projector=new MarketDataFeedRecoveryEventProjector(queue,fixture.Source,Substitute.For<IBlackboardService>(),databases,NullLogger<MarketDataFeedRecoveryEventProjector>.Instance,reliability);
            await projector.StartAsync(context,deadline.Token);
            try {
                while((await MarketDataRecoveryLog.QueryAsync(Connection,record.ValueDate,record.Dataset,record.OperationId,token:deadline.Token)).Count==0) await Task.Delay(10,deadline.Token);
                await fixture.WaitForCheckpoint(fact.EventId,deadline.Token);
            } finally {await projector.StopAsync();}
        }
        // A fresh projector and broker connection replay no duplicate business row.
        await using(var queue=new NatsJSDurableReplayQueue(options)) {
            var projector=new MarketDataFeedRecoveryEventProjector(queue,fixture.Source,Substitute.For<IBlackboardService>(),databases,NullLogger<MarketDataFeedRecoveryEventProjector>.Instance,reliability);
            await projector.StartAsync(context,deadline.Token);
            try {await projector.ProcessDomainEventAsync(fact);Assert.Single(await MarketDataRecoveryLog.QueryAsync(Connection,record.ValueDate,record.Dataset,record.OperationId));}
            finally {await projector.StopAsync();}
        }
    }
    [Fact]
    public async Task Expired_storage_attempt_does_not_poison_a_fresh_retry()
    {
        await using var fixture=await Fixture.Create();var record=Start();var request=Request(record);
        var expired=await fixture.Record(request with {AttemptStartedAtUtc=DateTime.UtcNow.AddMinutes(-3),RequestDeadlineUtc=DateTime.UtcNow.AddSeconds(-1)});
        Assert.True(expired.IsFailed);Assert.Empty(fixture.Facts);
        var retry=await fixture.Record(Request(record));Assert.True(retry.IsCompleted,retry.Failed?.ErrorData);
        Assert.Equal(1,await fixture.Count(request.StreamId));Assert.Single(fixture.Facts);
    }
    sealed class Fixture : IAsyncDisposable
    {
        public required EventSourceActorDbContext Source {get;init;}
        public required MarketDataFeedRecoveryStateRepository Repository {get;init;}
        public required MarketDataFeedRecoveryCommandActor CommandActor {get;init;}
        public required MarketDataFeedRecoveryRecordFunctionActor FunctionActor {get;init;}
        public List<MarketDataFeedRecoveryFact> Facts {get;}=[];
        public static async Task<Fixture> Create(ILoggerFactory? logging=null)
        {
            logging ??= NullLoggerFactory.Instance;
            var settings=new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,Connection,"System.Data.Postgres");
            await new EventSourceSchemaDb(settings,NullLogger<DbProvider>.Instance).CreateAllAsync();
            await using(var database=new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(Connection)){await database.OpenAsync();await using var sql=new NpgsqlCommand(MarketDataServiceSchemaSql.CreateSchema+MarketDataServiceSchemaSql.CreateRecoveryLog,database);await sql.ExecuteNonQueryAsync();}
            var dbs=Substitute.For<IDbContextFactory>();var blackboard=Substitute.For<IBlackboardService>();
            blackboard.EventSourcing.EventStreamId.Returns(new EventStreamIdCacheModel(Substitute.For<IRedisCache>(),new SystemTextJsonSerializer()));
            var source=new EventSourceActorDbContext(settings,dbs,blackboard,NullLogger<DbProvider>.Instance);
            dbs.ActorEventSourceDb.Returns(source);
            var factory=Substitute.For<IEventSourceActorStateFactory>();factory.CreateState<MarketDataFeedRecoveryState>().Returns(_=>new MarketDataFeedRecoveryState());
            var actors=Substitute.For<IActorService>();var projector=Substitute.For<IEventProjector<MarketDataFeedRecoveryCommandActor>>();
            var repo=new MarketDataFeedRecoveryStateRepository(factory,source,actors,projector,logging.CreateLogger<MarketDataFeedRecoveryStateRepository>());
            var commandContext=Substitute.For<ICommandActorContext<MarketDataFeedRecoveryCommandActor>>();commandContext.ActorId.Returns(new ActorMailboxId(ActorType.Command,MarketDataFeedRecoveryCommandActor.ActorName));
            var container=Substitute.For<IContainerInstance>();var audit=Substitute.For<ICommandAuditLogger>();audit.TryReserveAsync(Arg.Any<ICommand>(),Arg.Any<CancellationToken>()).Returns(new CommandAuditReservation(true));container.Resolve<ICommandAuditLogger>().Returns(audit);commandContext.Container.Returns(container);
            var commandActor=new MarketDataFeedRecoveryCommandActor(commandContext,repo,projector,logging.CreateLogger<MarketDataFeedRecoveryCommandActor>());
            var supervisor=Substitute.For<IActorSupervisor>();supervisor.CreateMailbox(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorMailbox>());supervisor.GetProducer(Arg.Any<ActorMailboxId>()).Returns(Substitute.For<IActorProducer>());await commandActor.StartAsync(supervisor);
            var functionContext=Substitute.For<IFunctionActorContext<MarketDataFeedRecoveryRecordFunctionActor>>();functionContext.ActorId.Returns(new ActorMailboxId(ActorType.Function,MarketDataFeedRecoveryRecordFunctionActor.ActorName));
            var functionRepo=new MarketDataFeedRecoveryRecordRepository(factory,source,actors,logging.CreateLogger<MarketDataFeedRecoveryRecordRepository>());
            var function=new MarketDataFeedRecoveryRecordFunctionActor(functionContext,functionRepo,actors,logging.CreateLogger<MarketDataFeedRecoveryRecordFunctionActor>());
            var fixture=new Fixture {Source=source,Repository=repo,CommandActor=commandActor,FunctionActor=function};
            projector.DomainEventsProjectionAsync(Arg.Any<DomainEventCollection>()).Returns(call=> {fixture.Facts.AddRange(call.Arg<DomainEventCollection>().OfType<MarketDataFeedRecoveryFact>());return ValueTask.CompletedTask;});
            actors.RequestAsync<AppendMarketDataFeedRecoveryCommand,MarketDataFeedRecoveryId>(Arg.Any<AppendMarketDataFeedRecoveryCommand>(),Arg.Any<CancellationToken>()).Returns(call=>new ValueTask<ServiceResult<Guid>>(Forward(call.Arg<AppendMarketDataFeedRecoveryCommand>())));
            async Task<ServiceResult<Guid>> Forward(AppendMarketDataFeedRecoveryCommand command) {var result=await fixture.Command(command);return new ServiceResult<Guid> {Success=result.Success,Value=command.CommandId,ErrorMessage=result.ErrorMessage};}
            return fixture;
        }
        public AppendMarketDataFeedRecoveryCommand Append(RecordMarketDataFeedRecoveryCommand request)
        {var record=RecoveryRecord.Parse(request.Payload,request.Digest);return new() {CommandId=request.CommandId,EntityId=new(record.OperationId),Subject=new(ActorType.Command,AppendMarketDataFeedRecoveryCommand.Actor,"Record",record.OperationId.ToString()),Payload=request.Payload,Digest=request.Digest,AttemptId=request.AttemptId,AttemptStartedAtUtc=request.AttemptStartedAtUtc,RequestDeadlineUtc=request.RequestDeadlineUtc};}
        public async Task<ServiceResult<GuidResult>> Command(AppendMarketDataFeedRecoveryCommand command) {var message=new Message(command);await CommandActor.HandleMessageAsync(message);return (ServiceResult<GuidResult>)message.Reply!;}
        public async Task<FunctionResult<MarketDataFeedRecoveryRecordStoredEvent,MarketDataFeedRecoveryRecordStorageFailedEvent>> Record(RecordMarketDataFeedRecoveryCommand command)
        {var message=new Message(command);await FunctionActor.HandleMessageAsync(message);return ((ServiceResult<FunctionResult<MarketDataFeedRecoveryRecordStoredEvent,MarketDataFeedRecoveryRecordStorageFailedEvent>>)message.Reply!).Value!;}
        public async Task WaitForCheckpoint(long eventId,CancellationToken token)
        {
            await using var connection=new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(Connection);await connection.OpenAsync(token);
            while(true) {
                await using var command=new NpgsqlCommand("SELECT count(*) FROM event_projector_stream_checkpoint WHERE projectorname=@projector AND lastappliedeventid=@event",connection);
                command.Parameters.AddWithValue("projector",nameof(MarketDataFeedRecoveryEventProjector));command.Parameters.AddWithValue("event",eventId);
                if(Convert.ToInt32(await command.ExecuteScalarAsync(token))>0) return;
                await Task.Delay(10,token);
            }
        }
        public async Task<long> Count(string stream) {await using var connection=new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(Connection);await connection.OpenAsync();await using var command=new NpgsqlCommand("SELECT count(*) FROM event_log l JOIN event_stream_id s ON l.eventstreamid=s.eventstreamid WHERE s.eventstream=$1",connection);command.Parameters.AddWithValue(stream);return (long)(await command.ExecuteScalarAsync())!;}
        public async Task Sql(string sql) {await using var connection=new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(Connection);await connection.OpenAsync();await using var command=new NpgsqlCommand(sql,connection);await command.ExecuteNonQueryAsync();}
        public async ValueTask DisposeAsync() {await CommandActor.StopAsync();await Source.DisposeAsync();}
    }
    sealed class Message(ICommand request) : IActorMessage
    {
        public object? Reply {get;private set;}
        public ActorSubject Subject=>request.Subject;
        public ActorSubject ReplySubject {get;set;}
        public T? AsCommand<T>() where T:class,ICommand=>request as T;
        public T? AsEvent<T>() where T:class,IEvent=>default;
        public T? AsQuery<T,TResult>() where T:class,IQuery<TResult> where TResult:class=>default;
        public ValueTask ReplyAsync<T>(T result) where T:class {Reply=result;return ValueTask.CompletedTask;}
        public void ReleasePayload(){}
        public NatsMsg<byte[]> GetMessage()=>default;
        public void Dispose(){}
    }
}
