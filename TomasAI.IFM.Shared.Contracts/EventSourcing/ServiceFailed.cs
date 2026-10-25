using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.EventSourcing;

[MessagePackObject(AllowPrivate = true)]
public class ServiceFailed : ServiceResult
{
    public ServiceFailed() : base() { }

    public ServiceFailed(int errorCode, string errorMessage)
        : base(false, errorCode, errorMessage) { }

    [SerializationConstructor]
    public ServiceFailed(bool success, int errorCode, string errorMessage) : base(success, errorCode, errorMessage) { }
}

[MessagePackObject(AllowPrivate = true)]
public class ServiceFailed<TResult> : ServiceResult<TResult>
{
    public ServiceFailed() : base() { }

    [SerializationConstructor]
    public ServiceFailed(bool success, int errorCode, string errorMessage, TResult? value)
        : base(success, errorCode, errorMessage, value) { }

    public ServiceFailed(int errorCode, string errorMessage, TResult? value)
        : base(false, errorCode, errorMessage, value) { }

    public ServiceFailed(int errorCode, string errorMessage) : base(errorCode, errorMessage) { }

    public ServiceFailed(IErrorEvent errorEvent) : base(errorEvent) { }
}
