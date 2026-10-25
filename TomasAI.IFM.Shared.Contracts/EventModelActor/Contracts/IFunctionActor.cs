namespace TomasAI.IFM.Shared.EventModelActor.Contracts;

/// <summary>Identifies a request/reply actor whose only durable state transition is a completed function result.</summary>
public interface IFunctionActor : IActor;

/// <summary>Closed-generic Function actor marker used by actor registration.</summary>
public interface IFunctionActor<TActor> : IFunctionActor, IActor<TActor>
    where TActor : IActor;
