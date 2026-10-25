using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.EventSourcing;

[MessagePackObject(AllowPrivate = true)]
public class ServiceOk : ServiceResult
{
    public ServiceOk() : base() { }

    [SerializationConstructor]
    public ServiceOk(bool success, int errorCode, string errorMessage) : base(success, errorCode, errorMessage) { }
}

[MessagePackObject(AllowPrivate = true)]
public class ServiceOk<TResult> : ServiceResult<TResult>
{
    public ServiceOk() : base() { }
    public ServiceOk(TResult value) : base(value) { }

    [SerializationConstructor]
    public ServiceOk(bool success, int errorCode, string errorMessage, TResult? value)
        : base(success, errorCode, errorMessage, value) { }
}
