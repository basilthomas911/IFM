using System.Diagnostics;
using System.Text.Json;
using MessagePack;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using Npgsql;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Application.Api.Server.Benchmarks.RealtimeMailboxes;

public static class WorkflowReservationVerification
{
    static readonly NatsByteArrayMessageSerializer Serializer=new();
    public static async Task Verify()
    {
        await using var infra=new IsolatedIntegrationInfrastructure("workflowreservation",includeCql:false);await infra.StartAsync();
        var cs=new NpgsqlConnectionStringBuilder(infra.PostgresConnectionString){Username=infra.PostgresUser,Password=infra.PostgresPassword}.ConnectionString;
        await using var pg=new NpgsqlConnection(cs);await pg.OpenAsync();
        var root=Path.Combine("C:/repos/IFM/.artifacts/realtime-mailboxes/20261124-safety/workflow-reservation-crashes",Guid.NewGuid().ToString("N"));
        await using(var create=new NpgsqlCommand("CREATE TABLE workflow_reservation_receipts(ordinal bigserial,scenario text NOT NULL,command_id uuid NOT NULL,source_id uuid NOT NULL,payload bytea NOT NULL,PRIMARY KEY(scenario,command_id))",pg))await create.ExecuteNonQueryAsync();
        foreach(var storage in new[]{StreamConfigStorage.Memory,StreamConfigStorage.File})
        foreach(var mode in new[]{"send-failure","typed-rejection","lost-reply","accepted-before-marker","completed-no-ack"})
        {
            var name=$"RES_{storage}_{mode.Replace('-','_')}";var subject="workflowreservation."+name;
            await using var connection=new NatsConnection(new NatsOpts{Url=infra.NatsUrl});var js=new NatsJSContext(connection);
            await js.CreateStreamAsync(new StreamConfig(name,[subject]){Storage=storage,Retention=StreamConfigRetention.Workqueue,MaxMsgs=16,MaxBytes=1024*1024,Discard=StreamConfigDiscard.New});
            await js.CreateOrUpdateConsumerAsync(name,new ConsumerConfig("ordered"){FilterSubject=subject,AckPolicy=ConsumerConfigAckPolicy.Explicit,MaxAckPending=1,AckWait=TimeSpan.FromSeconds(3),MaxDeliver=-1});
            foreach(var seq in new[]{1,2})(await js.PublishAsync(subject,MessagePackSerializer.Serialize(WorkflowDispatchDeliveryFixture.ReservationInput(seq)),serializer:Serializer)).EnsureSuccess();
            var start=new ProcessStartInfo("dotnet"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};start.ArgumentList.Add(typeof(WorkflowReservationVerification).Assembly.Location);
            foreach(var arg in new[]{"--workflow-reservation-crash-child",name,mode})start.ArgumentList.Add(arg);
            start.Environment["IFM_RES_NATS"]=infra.NatsUrl;start.Environment["IFM_RES_PG"]=cs;start.Environment["IFM_RES_ROOT"]=root;
            using var child=Process.Start(start)!;var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();
            try{await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));}catch{child.Kill(true);throw;}
            Require(child.ExitCode==0&&(await stdout).Contains("RES_INPUT_RETAINED"),"Child failed: "+await stderr);
            using var journal=new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.WorkflowReservationFailures(Path.Combine(root,name));
            await using var state=new WorkflowDispatchDeliveryFixture(reservationFailures:journal);Bind(state,pg,name);state.Time.At=state.Time.At.AddMinutes(1);await state.Start();var consumer=await js.GetConsumerAsync(name,"ordered");
            foreach(var seq in new[]{1,2})
            {
                var input=await Fetch(consumer);var source=MessagePackSerializer.Deserialize<WorkflowStrategyStateUpdatedEvent>(input.Data!);
                Require(source.Id==WorkflowDispatchDeliveryFixture.ReservationInput(seq).Id,"Source order/identity changed.");
                state.ReadCurrent=_=>Task.FromResult<IntrinsicTimeStrategyWorkflowView?>(source.State);
                Require(await state.Deliver(source),"Retained workflow dispatch failed.");
                await input.AckAsync(new AckOpts{DoubleAck=true});
            }
            await consumer.RefreshAsync();Require(consumer.Info.NumPending==0&&consumer.Info.NumAckPending==0,"Unsettled workflow source.");
            await using(var read=new NpgsqlCommand("SELECT source_id FROM workflow_reservation_receipts WHERE scenario=@scenario ORDER BY ordinal",pg))
            {
                read.Parameters.AddWithValue("scenario",name);await using var reader=await read.ExecuteReaderAsync();var ids=new List<Guid>();while(await reader.ReadAsync())ids.Add(reader.GetGuid(0));
                Require(ids.SequenceEqual(new[]{WorkflowDispatchDeliveryFixture.ReservationInput(1).Id,WorkflowDispatchDeliveryFixture.ReservationInput(2).Id}),"Terminal receipt duplicated or reordered.");
            }
            Console.WriteLine($"PASS {storage}/{mode}: actual workflow actor/scheduler; original reservation rejection terminal bytes/IDs across clock-advanced cold startup and source replay; two ordered PostgreSQL receiver receipts; no pending source");
            await js.DeleteStreamAsync(name);
        }
        Console.WriteLine("10 reservation rejection abrupt API-child cases passed; real flushed terminal journal, actor startup recovery and retained source replay. Retained source driver is explicit Memory/File JetStream with MaxAckPending=1; broker stays running. PostgreSQL receiver is a synthetic idempotent terminal owner, not production workflow state/projectors or other pipeline result replay. No production durable-mailbox adapter or live activation.");
    }
    static void Bind(WorkflowDispatchDeliveryFixture state,NpgsqlConnection pg,string scenario)
    {
        state.Send=async message=>
        {
            var command=(FailTradeSelectionCommand)message;var bytes=JsonSerializer.SerializeToUtf8Bytes(command);
            await using(var insert=new NpgsqlCommand("INSERT INTO workflow_reservation_receipts(scenario,command_id,source_id,payload) VALUES(@scenario,@command,@source,@payload) ON CONFLICT DO NOTHING",pg))
            {insert.Parameters.AddWithValue("scenario",scenario);insert.Parameters.AddWithValue("command",command.CommandId);insert.Parameters.AddWithValue("source",command.SourceEventId);insert.Parameters.AddWithValue("payload",bytes);await insert.ExecuteNonQueryAsync();}
            await using var read=new NpgsqlCommand("SELECT payload FROM workflow_reservation_receipts WHERE scenario=@scenario AND command_id=@command",pg);
            read.Parameters.AddWithValue("scenario",scenario);read.Parameters.AddWithValue("command",command.CommandId);
            Require(((byte[])(await read.ExecuteScalarAsync())!).AsSpan().SequenceEqual(bytes),"Terminal command changed after uncertain dispatch.");
        };
    }
    public static async Task CrashChild(string name,string mode)
    {
        var pg=new NpgsqlConnection(Environment.GetEnvironmentVariable("IFM_RES_PG"));await pg.OpenAsync();
        var journal=new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.WorkflowReservationFailures(Path.Combine(Environment.GetEnvironmentVariable("IFM_RES_ROOT")!,name));
        var state=new WorkflowDispatchDeliveryFixture(reservationFailures:journal);Bind(state,pg,name);var accepted=state.Send;
        state.Send=async command=>
        {
            if(mode=="send-failure")throw new IOException("Before terminal acceptance.");
            if(mode=="typed-rejection")return;
            await accepted(command);if(mode=="accepted-before-marker"){Console.WriteLine("RES_INPUT_RETAINED");Environment.Exit(0);}if(mode=="lost-reply")throw new IOException("Terminal accepted; reply lost.");
        };
        state.TypedRejected=mode=="typed-rejection";await state.Start();
        var connection=new NatsConnection(new NatsOpts{Url=Environment.GetEnvironmentVariable("IFM_RES_NATS")!});var consumer=await new NatsJSContext(connection).GetConsumerAsync(name,"ordered");
        var input=await Fetch(consumer);var source=MessagePackSerializer.Deserialize<WorkflowStrategyStateUpdatedEvent>(input.Data!);
        state.ReadCurrent=_=>mode=="read-failure"?Task.FromException<IntrinsicTimeStrategyWorkflowView?>(new IOException("Required projection read failed.")):Task.FromResult<IntrinsicTimeStrategyWorkflowView?>(source.State);
        Require(await state.Deliver(source)==(mode=="completed-no-ack"),"Unexpected source settlement.");
        await foreach(var probe in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts{MaxMsgs=1,Expires=TimeSpan.FromMilliseconds(1100)},Serializer))
            Require(MessagePackSerializer.Deserialize<WorkflowStrategyStateUpdatedEvent>(probe.Data!).Id==source.Id,"Successor overtook unacknowledged workflow input.");
        Console.WriteLine("RES_INPUT_RETAINED");Environment.Exit(0);
    }
    static async Task<INatsJSMsg<byte[]>> Fetch(INatsJSConsumer consumer)
    {await foreach(var input in consumer.FetchAsync<byte[]>(new NatsJSFetchOpts{MaxMsgs=1,Expires=TimeSpan.FromSeconds(5)},Serializer))return input;throw new TimeoutException("Missing retained workflow source.");}
    static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
}
