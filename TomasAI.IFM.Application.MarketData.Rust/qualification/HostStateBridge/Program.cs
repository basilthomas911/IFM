using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Recovery;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Application.MarketData.Rust.Host;

var clock=TimeProvider.System;var factory=new NativeMarketDataEpochFactory(clock,(_,_,_)=>throw new Exception("No native bootstrap expected."));
var health=new OptionFeedProcessingHealth(clock);var log=new Captured();var state=new NativeHostState(factory,new DatasetWorkerAdmissionRegistry(),clock,log,health);
var date=new DateOnly(2026,10,26);state.PrepareDate(date);var old=state.Observers;var status=new LiveLaneStatus(3,1,1,1,Guid.NewGuid(),null);
for(var i=0;i<10000;i++)old.ScopeStatus("fixture",status);
if(log.Lifecycle!=1)throw new InvalidDataException("Unchanged status logged on every native tick.");
old.ScopeStatus("fixture",status with{Phase=4,Error="injected"});if(log.Lifecycle!=2||log.Last!=LogLevel.Warning)throw new InvalidDataException("Lifecycle transition/severity was lost.");
old.Fault(null,new InvalidOperationException("old failure"));if(health.Read(date,Guid.Empty) is null)throw new InvalidDataException("Processing failure was only logged.");
health.Recovered(date,Guid.Empty,DateTimeOffset.MaxValue);state.PrepareDate(date);
old.Fault(null,new InvalidOperationException("late retired failure"));if(health.Read(date,Guid.Empty)is not null)throw new InvalidDataException("Retired observer condemned its replacement.");
state.Observers.Fault(null,new InvalidOperationException("current failure"));if(health.Read(date,Guid.Empty)is null)throw new InvalidDataException("Current observer failure was fenced accidentally.");
health.Recovered(date,Guid.Empty,DateTimeOffset.MaxValue);state.CloseOptionsAdmission(date);state.Observers.Fault(null,new InvalidOperationException("intentional shutdown"));
if(health.Read(date,Guid.Empty)is not null)throw new InvalidDataException("Intentional shutdown entered live recovery.");
var brokenHealth=new OptionFeedProcessingHealth(clock);var broken=new NativeHostState(factory,new DatasetWorkerAdmissionRegistry(),clock,new Captured{Throw=true},brokenHealth);broken.PrepareDate(date);
broken.Observers.Fault(null,new IOException("original source fault"));if(brokenHealth.Read(date,Guid.Empty)is null)throw new InvalidDataException("Logger failure hid unhealthy state.");
Console.WriteLine("PASS Host observer state: 10000 identical ticks produce one lifecycle log; transitions/severity preserved; health retained before logging; retired epochs fenced; intentional shutdown excluded; throwing logger cannot hide failure.");
sealed class Captured:ILogger<NativeHostState>{public int Lifecycle;public LogLevel Last;public bool Throw;public bool IsEnabled(LogLevel level)=>true;public IDisposable? BeginScope<T>(T state)where T:notnull=>null;
public void Log<T>(LogLevel level,EventId id,T state,Exception? error,Func<T,Exception?,string> format){if(Throw)throw new IOException("logging fixture");Last=level;if(format(state,error).StartsWith("Native option lifecycle",StringComparison.Ordinal))Lifecycle++;}}
