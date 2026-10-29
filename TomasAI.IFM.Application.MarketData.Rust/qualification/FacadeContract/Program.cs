using System.Reflection;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Rust.Api;

// No provider, database, native runtime or Host is created. This gate guards the
// public boundary, not the still-required end-to-end operation qualification.
var api = typeof(IMarketDataApi);
var facade = typeof(RustMarketDataApi);
var mapping = facade.GetInterfaceMap(api);
foreach (var target in mapping.TargetMethods)
    if (target.DeclaringType != facade || target.IsAbstract)
        throw new InvalidOperationException($"API operation inherited an interface fallback: {target}.");

var required = typeof(IRustMarketDataHostServices).GetMethods();
if (required.Any(method => !method.IsAbstract || method.GetMethodBody() is not null))
    throw new InvalidOperationException("Host capabilities must have no default implementation.");

foreach (var method in api.GetMethods().Where(method => !method.IsSpecialName))
{
    var host = method.GetCustomAttribute<MarketDataHostOperationAttribute>() is not null
        || method.GetParameters().Any(p => p.ParameterType == typeof(SubscriptionAcquireRequest))
        || method.Name is "RenewSubscriptionLeaseAsync" or "ReleaseSubscriptionLeaseAsync" or "AcquireSelectedSubscriptionLeasesAsync" or "GetSubscriptionLeasesAsync";
    var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
    var capability = typeof(IRustMarketDataHostServices).GetMethod(method.Name, parameters);
    var appCapability=typeof(IRustMarketDataApplicationServices).GetMethod(method.Name,parameters);
    if(host==(appCapability is not null) || appCapability is not null && (appCapability.ReturnType!=method.ReturnType || !appCapability.IsAbstract))
        throw new InvalidOperationException($"Required native application routing contract drift: {method}.");
    if (host != (capability is not null) || capability is not null && capability.ReturnType != method.ReturnType)
        throw new InvalidOperationException($"Host routing contract drift: {method}.");
}

var constructor = facade.GetConstructors().Single(c=>c.GetParameters()[0].ParameterType==typeof(IRustMarketDataEpochFactory));
if (constructor.GetParameters()[0].ParameterType != typeof(IRustMarketDataEpochFactory)
    || constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(IDatabentoMarketDataEpochFactory)))
    throw new InvalidOperationException("Facade must require its distinct native-only epoch factory.");
if (!typeof(IDatabentoMarketDataEpoch).IsAssignableFrom(typeof(IRustMarketDataEpoch)))
    throw new InvalidOperationException("Native epochs no longer satisfy the existing application lifecycle contract.");
Console.WriteLine($"PASS: {mapping.TargetMethods.Length} explicit API/property routes, {required.Length} abstract Host capabilities, native-only epoch factory and existing lifecycle boundary. This is contract coverage, not full backend acceptance.");

// Every concrete native-owner route is tested without creating a runtime,
// epoch, lease, network client, timer, database or application-policy object.
var app=DispatchProxy.Create<IRustMarketDataApplicationServices,RoutingProbe>();
var hostServices=DispatchProxy.Create<IRustMarketDataHostServices,RoutingProbe>();
var appProbe=(RoutingProbe)(object)app;var hostProbe=(RoutingProbe)(object)hostServices;
var ownerFacade=new RustMarketDataApi(app,hostServices);
foreach(var method in api.GetMethods().Where(method=>!method.IsSpecialName))
{
    appProbe.Last=null;hostProbe.Last=null;
    var hostRoute=typeof(IRustMarketDataHostServices).GetMethod(method.Name,method.GetParameters().Select(p=>p.ParameterType).ToArray()) is not null;
    var arguments=method.GetParameters().Select(parameter=>
    {
        var type=parameter.ParameterType.IsByRef?parameter.ParameterType.GetElementType()!:parameter.ParameterType;
        return type.IsValueType?Activator.CreateInstance(type):null;
    }).ToArray();
    var result=method.Invoke(ownerFacade,arguments);if(result is Task task)await task;
    var expected=hostRoute?hostProbe:appProbe;var other=hostRoute?appProbe:hostProbe;
    if(expected.Last is not {} routed || routed.Name!=method.Name || routed.ReturnType!=method.ReturnType || other.Last is not null)
        throw new InvalidOperationException("Native facade did not directly forward the required command/query: "+method);
}
appProbe.Last=null;hostProbe.Last=null;await ownerFacade.DisposeAsync();
if(appProbe.Last?.Name!=nameof(IAsyncDisposable.DisposeAsync)||hostProbe.Last is not null)
    throw new InvalidOperationException("Native facade disposal entered the prototype lifecycle.");
Console.WriteLine("PASS: all 71 native-owner command/query routes directly forward to required adapters, including start/stop/dispose without a managed lifecycle gate.");

public class RoutingProbe:DispatchProxy
{
    public MethodInfo? Last{get;set;}
    protected override object? Invoke(MethodInfo? targetMethod,object?[]? args)
    {
        Last=targetMethod??throw new InvalidOperationException("Missing probe target.");
        var parameters=targetMethod.GetParameters();
        for(var index=0;index<parameters.Length;index++)if(parameters[index].IsOut)
        {
            var type=parameters[index].ParameterType.GetElementType()!;
            args![index]=type.IsValueType?Activator.CreateInstance(type):null;
        }
        var result=targetMethod.ReturnType;
        if(result==typeof(Task))return Task.CompletedTask;
        if(result==typeof(ValueTask))return ValueTask.CompletedTask;
        if(result.IsGenericType&&result.GetGenericTypeDefinition()==typeof(Task<>))
        {
            var value=result.GenericTypeArguments[0];
            return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(value)
                .Invoke(null,[value.IsValueType?Activator.CreateInstance(value):null]);
        }
        return result.IsValueType?Activator.CreateInstance(result):null;
    }
}
