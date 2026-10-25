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

    public ServiceResult(bool success, int errorCode, string? errorMessage = null, bool unusedForOverload = false)
    {
        Success = success;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage ?? string.Empty;
    }
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
        : base(false, (errorEvent ?? throw new ArgumentNullException(nameof(errorEvent))).ErrorCode, errorEvent.ErrorMessage)
    {
        ArgumentNullException.ThrowIfNull(errorEvent);
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
