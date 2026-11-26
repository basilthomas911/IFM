using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;

/// <summary>Process-local code/serialization readiness. It owns no market input, workflow result or financial authority.</summary>
public sealed class WorkflowRuntimePreparation
{
    public sealed record Result(int PreparedMethods,int PreparedContracts,TimeSpan Elapsed);
    readonly Lazy<Task<Result>> preparation;
    Result? ready;Exception? failure;
    public bool IsReady=>Volatile.Read(ref ready) is not null;
    public Result? Prepared=>Volatile.Read(ref ready);
    public string? Failure=>Volatile.Read(ref failure)?.Message;
    public WorkflowRuntimePreparation(Domain.Portfolio.Shared.OrderComposition.IPortfolioEntryDecisionCalculator? portfolioCalculator=null)
        :this(token=>PrepareCodeAsync(portfolioCalculator?.GetType().Assembly,token)){}
    internal WorkflowRuntimePreparation(Func<CancellationToken,Task<Result>> prepare)
    {preparation=new(()=>RunAsync(prepare),LazyThreadSafetyMode.ExecutionAndPublication);}
    public Task<Result> PrepareAsync(CancellationToken token=default)=>preparation.Value.WaitAsync(token);
    public void RequireReady()
    {if(!IsReady)throw new InvalidOperationException(Failure is null?"WF.DECISION.RUNTIME_NOT_PREPARED":"WF.DECISION.RUNTIME_PREPARATION_FAILED: "+Failure);}
    async Task<Result> RunAsync(Func<CancellationToken,Task<Result>> prepare)
    {
        try{var result=await prepare(CancellationToken.None).ConfigureAwait(false);Volatile.Write(ref ready,result);return result;}
        catch(Exception error){Volatile.Write(ref failure,error);throw;}
    }
    static Task<Result> PrepareCodeAsync(Assembly? financialAssembly,CancellationToken token)=>Task.Run(()=>
    {
        var timer=Stopwatch.StartNew();var methods=0;var contracts=0;
        var types=typeof(WorkflowCalculationRuntime).Assembly.GetTypes().Concat(financialAssembly?.GetTypes()??[]).Where(type=>
            (type.FullName?.StartsWith("TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.",StringComparison.Ordinal)==true
            ||type.FullName?.StartsWith("TomasAI.IFM.Domain.Portfolio.OrderComposition.",StringComparison.Ordinal)==true)
            && !type.ContainsGenericParameters).ToArray();
        foreach(var type in types)
        foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly))
        {
            token.ThrowIfCancellationRequested();
            if(method.IsAbstract||method.ContainsGenericParameters||method.GetMethodBody() is null)continue;
            if(++methods>20000)throw new InvalidOperationException("Workflow startup method preparation exceeds its fixed scope.");
            RuntimeHelpers.PrepareMethod(method.MethodHandle);
        }
        foreach(var actor in types.Where(type=>type.FullName!.StartsWith("TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.",StringComparison.Ordinal)
            &&(type.Name is "IntrinsicTimeStrategyWorkflowCommandActor" or "IntrinsicTimeStrategyWorkflowRealtimeActor"
            ||type.Name.EndsWith("FunctionActor",StringComparison.Ordinal)&&type.Namespace?.Contains(".Function.Actor.",StringComparison.Ordinal)==true)))
        {
            // These narrowly selected initializers only construct immutable
            // route/delegate maps. No actor instance or handler is invoked.
            RuntimeHelpers.RunClassConstructor(actor.TypeHandle);
            if(actor.BaseType is not{} parent)continue;
            foreach(var method in parent.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly))
            {
                var candidate=method;
                if(method.IsGenericMethodDefinition&&method.Name=="DispatchMappedFunctionEvent"&&method.GetGenericArguments().Length==1)
                    candidate=method.MakeGenericMethod(typeof(TimeProvider));
                if(!candidate.ContainsGenericParameters&&candidate.GetMethodBody() is not null){RuntimeHelpers.PrepareMethod(candidate.MethodHandle);methods++;}
            }
        }
        // Open generic async bodies are not compiled by the ordinary type walk.
        // Prepare the six real calculator shapes, including their MoveNext code,
        // without invoking any calculator or manufacturing accepted evidence.
        var runtime=typeof(WorkflowCalculationRuntime);
        var shapes=new(Type Input,Type Output)[]
        {
            (typeof(RegimeDiscovery.Model.RegimeDiscoveryCalculationInput),typeof(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model.RegimeDiscoveryResult)),
            (runtime.GetNestedType("AssessmentInput",BindingFlags.NonPublic)!,typeof(MarketConditionAssessmentResult)),
            (runtime.GetNestedType("SelectionInput",BindingFlags.NonPublic)!,typeof(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionResult)),
            (runtime.GetNestedType("CompositionInput",BindingFlags.NonPublic)!,typeof(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.OrderCompositionResult)),
            (typeof(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteRiskManagementPipelineCommand),typeof(Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement.RiskAssessmentResult)),
            (runtime.GetNestedType("PortfolioInput",BindingFlags.NonPublic)!,typeof(Domain.Portfolio.Shared.OrderComposition.PortfolioEntryApproval))
        };
        foreach(var shape in shapes)
        {
        foreach(var nested in runtime.GetNestedTypes(BindingFlags.NonPublic).Concat(typeof(WorkflowDecisionExecutor).GetNestedTypes(BindingFlags.NonPublic))
            .Where(type=>type.IsGenericTypeDefinition&&type.GetGenericArguments().Length==2))
        {
            var closed=nested.MakeGenericType(shape.Input,shape.Output);
            foreach(var method in closed.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly))
                if(!method.ContainsGenericParameters&&method.GetMethodBody() is not null){RuntimeHelpers.PrepareMethod(method.MethodHandle);methods++;}
            foreach(var constructor in closed.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                if(constructor.GetMethodBody() is not null){RuntimeHelpers.PrepareMethod(constructor.MethodHandle);methods++;}
        }
        foreach(var method in runtime.GetMethods(BindingFlags.NonPublic|BindingFlags.Instance).Concat(typeof(WorkflowDecisionExecutor).GetMethods(BindingFlags.Public|BindingFlags.Instance))
            .Where(method=>method.IsGenericMethodDefinition&&method.GetGenericArguments().Length==2&&method.Name is "RunAsync" or "CalculateOnceAsync" or "ExecuteAsync"))
        {
            var closed=method.MakeGenericMethod(shape.Input,shape.Output);RuntimeHelpers.PrepareMethod(closed.MethodHandle);methods++;
            if(method.GetCustomAttribute<AsyncStateMachineAttribute>() is{} machine)
            {
                var state=machine.StateMachineType;
                if(state.ContainsGenericParameters)state=state.MakeGenericType(shape.Input,shape.Output);
                RuntimeHelpers.PrepareMethod(state.GetMethod("MoveNext",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)!.MethodHandle);methods++;
            }
        }
        }
        var prepare=typeof(WorkflowRuntimePreparation).GetMethod(nameof(PrepareContract),BindingFlags.NonPublic|BindingFlags.Static)!;
        foreach(var type in typeof(IntrinsicTimeStrategyWorkflowView).Assembly.GetTypes()
            .Concat(typeof(Domain.Portfolio.Shared.OrderComposition.PortfolioEntryApproval).Assembly.GetTypes())
            .Concat(typeof(Application.MarketData.Pricing.CompositionPreparation).Assembly.GetTypes()).Distinct().Where(type=>
            (type.FullName?.StartsWith("TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.",StringComparison.Ordinal)==true
            ||type.FullName?.StartsWith("TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.",StringComparison.Ordinal)==true
            ||type.Namespace=="TomasAI.IFM.Application.MarketData.Pricing"||type.Namespace=="TomasAI.IFM.Shared.MarketData.Contracts.Pricing")
            && !type.ContainsGenericParameters&&!type.IsAbstract&&type.GetCustomAttribute<MessagePackObjectAttribute>() is not null))
        {
            if(++contracts>2048)throw new InvalidOperationException("Workflow startup contract preparation exceeds its fixed scope.");
            try{prepare.MakeGenericMethod(type).Invoke(null,null);}
            catch(TargetInvocationException error){throw new InvalidOperationException("Workflow contract preparation failed: "+type.FullName,error.InnerException??error);}
        }
        PrepareNestedEnvelopes();
        return new Result(methods,contracts,timer.Elapsed);
    },token);
    static void PrepareContract<T>()
    {
        PrepareFormatter<T>();PrepareFormatter<T[]>();
        MarketConditionAssessmentHash.PrepareContract(typeof(T));
        Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.CompositionSemanticHash.PrepareContract(typeof(T));
        // Exercise serialization infrastructure on empty contracts only. These
        // objects are never validated as observations, admitted or published.
        // Metadata/JIT preparation alone does not initialize writer/pool paths.
        var type=typeof(T);
        if(type.GetConstructor(Type.EmptyTypes) is not null)
        {
            var empty=Activator.CreateInstance<T>();
            foreach(var options in new[]{MessagePackSerializer.DefaultOptions,Framework.Serialization.MessagePackBinarySerializer.Options})
            {
                var bytes=MessagePackSerializer.Serialize(empty,options);
                _=MessagePackSerializer.Deserialize<T>(bytes,options);
                var elements=MessagePackSerializer.Serialize(new[]{empty},options);
                _=MessagePackSerializer.Deserialize<T[]>(elements,options);
            }
            if(type.Name.EndsWith("Command",StringComparison.Ordinal)||type.Name.EndsWith("Result",StringComparison.Ordinal)
                ||type==typeof(IntrinsicTimeStrategyWorkflowView)||type==typeof(StrategyStageResultEnvelope))
            {
                _=MarketConditionAssessmentHash.Compute(empty);
                _=MarketConditionAssessmentHash.Compute(new[]{empty});
                _=Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.CompositionSemanticHash.Compute(empty);
                _=Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.CompositionSemanticHash.Compute(new[]{empty});
            }
        }
    }
    static void PrepareFormatter<T>()
    {
        foreach(var options in new[]{MessagePackSerializer.DefaultOptions,Framework.Serialization.MessagePackBinarySerializer.Options})
        {
        var formatter=options.Resolver.GetFormatter<T>()
            ??throw new InvalidOperationException("Missing workflow formatter: "+typeof(T).FullName);
        foreach(var method in formatter.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly))
            if(!method.ContainsGenericParameters&&method.GetMethodBody() is not null)RuntimeHelpers.PrepareMethod(method.MethodHandle);
        }
    }
    static void PrepareNestedEnvelopes()
    {
        // Serialization-only shapes exercise non-null result setter/callback and
        // fingerprint paths. They are deliberately not valid market decisions,
        // have no identities, and cannot reach an owner, input cache or transport.
        var selection=new Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionResult
            {DecisionContext=new(){SelectionBinding=new()}};
        var view=new IntrinsicTimeStrategyWorkflowView
        {
            RegimeDiscovery=new(){Result=new(){ContentType=StrategyStageResultEnvelope.TypedRegimeContentTypeV2,
                RegimeResult=new()}},
            MarketCondition=new(){Result=new(){AssessmentResult=new()}},
            TradeSelection=new(){Result=new(){ContentType=StrategyStageResultEnvelope.BinarySelectionContentType,
                SelectionResult=selection}},
            OrderComposition=new(){Result=new(){CompositionResult=new()}},
            RiskManagement=new(){Result=new(){RiskResult=new()}},
            SelectionBinding=new()
        };
        var bytes=Framework.Serialization.MessagePackBinarySerializer.Shared.Serialize(view);
        _=Framework.Serialization.MessagePackBinarySerializer.Shared.Deserialize<IntrinsicTimeStrategyWorkflowView>(bytes);
        _=MarketConditionAssessmentHash.Compute(view);
        var legacy=new StrategyStageResultEnvelope{ContentType=StrategyStageResultEnvelope.TypedSelectionContentType,SelectionResult=selection};
        _=MessagePackSerializer.Deserialize<StrategyStageResultEnvelope>(MessagePackSerializer.Serialize(legacy));
        var fingerprintOnly=new Domain.Portfolio.Shared.OrderComposition.PortfolioEntryApproval().Seal();
        _=fingerprintOnly.HasValidFingerprint();
        _=MessagePackSerializer.Deserialize<Domain.Portfolio.Shared.OrderComposition.PortfolioEntryApproval>(MessagePackSerializer.Serialize(fingerprintOnly));
        var captureShape=new Application.MarketData.Pricing.CompositionPreparation(0,new(Guid.Empty,0,""),"",
            new(Guid.Empty,"","",Guid.Empty,default,default,false),
            new(0,Guid.Empty,"","","",Guid.Empty,default,default,[],""),default,"");
        var captureBytes=Framework.Serialization.MessagePackBinarySerializer.Shared.Serialize(captureShape);
        _=Framework.Serialization.MessagePackBinarySerializer.Shared.Deserialize<Application.MarketData.Pricing.CompositionPreparation>(captureBytes);
    }
}
