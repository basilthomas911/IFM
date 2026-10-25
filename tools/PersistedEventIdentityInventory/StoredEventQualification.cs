using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Application.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

static class StoredEventQualification
{
 public static async Task RunAsync(string database,string destination,string? previous)
 {
  Result[]? baseline=previous is null?null:JsonSerializer.Deserialize<Result[]>(File.ReadAllText(previous));
  var selection=baseline is null?"" : " WHERE e.eventversion IN ("+string.Join(',',baseline.Select(r=>r.EventVersion))+")";
  var sql=baseline is null ? "SELECT json_build_object('eventVersion',e.eventversion,'type',n.eventtypename,'payload',encode(e.eventpayload,'hex')) FROM event_name_id n CROSS JOIN LATERAL (SELECT eventversion,eventpayload FROM event_log l WHERE l.eventnameid=n.eventnameid ORDER BY l.eventversion DESC LIMIT 1) e ORDER BY e.eventversion;"
    : "SELECT json_build_object('eventVersion',e.eventversion,'type',n.eventtypename,'payload',encode(e.eventpayload,'hex')) FROM event_log e JOIN event_name_id n USING(eventnameid)"+selection+" ORDER BY e.eventversion;";
  var start=new ProcessStartInfo("docker"){RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
  foreach(var arg in new[]{"exec","ifm_db","psql","-U","postgres","-d",database,"-Atc","BEGIN READ ONLY; SET LOCAL statement_timeout='60s'; "+sql+" COMMIT;"})start.ArgumentList.Add(arg);
  using var process=Process.Start(start)!;
  var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
  await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2));
  var text=await stdout;
  if(process.ExitCode!=0)throw new InvalidOperationException("Read-only stored-event query failed: "+await stderr);
  var results=new List<Result>();
  foreach(var line in text.Split('\n').Where(l=>l.TrimStart().StartsWith('{')))
  {
   using var document=JsonDocument.Parse(line);var row=document.RootElement;
   var id=row.GetProperty("eventVersion").GetInt64();var name=row.GetProperty("type").GetString()!;var bytes=Convert.FromHexString(row.GetProperty("payload").GetString()!);
   var hash=Convert.ToHexString(SHA256.HashData(bytes));
   try
   {
    var value=new EventStreamReadModel{EventVersion=id,EventTypeName=name,EventData=bytes}.ToDomainEvent();
    var result=value is UnknownEvent?"Unknown":"Decoded";
    EventInitHelper.SetProperty(value,nameof(IEvent.EventId),0L);
    var semantic=Convert.ToHexString(SHA256.HashData(EventLogMessagePackCodec.Shared.Serialize(value)));
    results.Add(new(id,name,result,value.GetType().FullName!,hash,semantic));
   }
   catch(Exception error){results.Add(new(id,name,"Failed",error.GetType().FullName!,hash,""));}
  }
  File.WriteAllText(destination,JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
  foreach(var group in results.GroupBy(r=>r.Outcome))Console.WriteLine($"{group.Key}: {group.Count()} stored event types");
  if(baseline is not null)
  {
   var current=results.ToDictionary(r=>r.EventVersion);
   foreach(var before in baseline)
    if(!current.TryGetValue(before.EventVersion,out var after) || before!=after)throw new InvalidOperationException($"Historical replay changed for event {before.EventVersion}: {before.EventTypeName}");
   Console.WriteLine($"PASS: {baseline.Length} exact stored records preserve source bytes, reconstructed payload hashes and read outcomes.");
  }
 }
 public sealed record Result(long EventVersion,string EventTypeName,string Outcome,string ResolvedType,string PayloadSha256,string ReconstructedSha256);
}
