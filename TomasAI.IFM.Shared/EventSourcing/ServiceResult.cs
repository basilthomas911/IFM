using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// create successful service result
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class ServiceResult
{
    [Key(0)] public bool Success { get; set; }
    [Key(1)] public int ErrorCode { get; set; }
    [Key(2)] public string ErrorMessage { get; set; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public ServiceResult() { }

    /// <summary>
    /// MessagePack serialization constructor matching keys 0..2
    /// </summary>
    [SerializationConstructor]
    public ServiceResult(bool success, int errorCode, string errorMessage)
    {
        Success = success;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage ?? string.Empty;
    }

    public ServiceResult(bool success, int errorCode, string? errorMessage = null, bool _unusedForOverload = false)
    {
        Success = success;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage ?? string.Empty;
    }
}

[MessagePackObject(AllowPrivate = true)]
public class ServiceOk : ServiceResult
{
    public ServiceOk() : base() { }

    [SerializationConstructor]
    public ServiceOk(bool success, int errorCode, string errorMessage) : base(success, errorCode, errorMessage) { }
}

[MessagePackObject(AllowPrivate = true)]
public class ServiceFailed : ServiceResult
{
    public ServiceFailed() : base() { }

    public ServiceFailed(int errorCode, string errorMessage)
        : base(false, errorCode, errorMessage) { }

    [SerializationConstructor]
    public ServiceFailed(bool success, int errorCode, string errorMessage) : base(success, errorCode, errorMessage) { }
}

/// <summary>
/// Generic service result that carries a value or an error event.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public class ServiceResult<TResult> : ServiceResult
{
    // base keys: 0..2
    // derived keys: 3
    [Key(3)] public TResult? Value { get; set; }

    [IgnoreMember]
    public IErrorEvent? ErrorEvent { get; set; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public ServiceResult() : base()
    {
        Value = default;
        ErrorEvent = default;
    }

    /// <summary>
    /// MessagePack & JSON constructor for deserialization. Parameter order matches keys 0..3.
    /// </summary>
    [SerializationConstructor]
    [JsonConstructor]
    public ServiceResult(bool success, int errorCode, string errorMessage, TResult? value)
        : base(success, errorCode, errorMessage)
    {
        Value = value;
        ErrorEvent = default;
    }

    public ServiceResult(TResult value)
        : base(true, 0, string.Empty)
    {
        Value = value;
        ErrorEvent = default;
    }

    public ServiceResult(IErrorEvent errorEvent)
        : base(false, errorEvent.ErrorCode, errorEvent.ErrorMessage)
    {
        Value = default;
        ErrorEvent = errorEvent;
    }

    public ServiceResult(int errorCode, string errorMessage)
        : base(false, errorCode, errorMessage)
    {
        Value = default;
        ErrorEvent = default;
    }
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

[MessagePackObject(AllowPrivate = true)]
public class ServiceFailed<TResult> : ServiceResult<TResult>
{
    public ServiceFailed() : base() { }

    [SerializationConstructor]
    public ServiceFailed(bool success, int errorCode, string errorMessage, TResult? value)
        : base(success, errorCode, errorMessage, value) { }

    public ServiceFailed( int errorCode, string errorMessage, TResult? value)
        : base(false, errorCode, errorMessage, value) { }

    public ServiceFailed(int errorCode, string errorMessage) : base(errorCode, errorMessage) { }

    public ServiceFailed(IErrorEvent errorEvent) : base(errorEvent) { }
}

[MessagePackObject(AllowPrivate = true)]
public class RestApiErrorContent
{
    [Key(0)] public int ErrorCode { get; set; }
    [Key(1)] public string ErrorMessage { get; set; }

    public RestApiErrorContent() { }

    [SerializationConstructor]
    public RestApiErrorContent(int errorCode, string errorMessage)
    {
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }
}

[MessagePackObject]
public record GuidResult([property: Key(0)] Guid Guid);
