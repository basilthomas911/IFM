using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.EventSourcing;

/// <summary>
/// 
/// </summary>
public enum ErrorType
{
    Undefined,
    Command,
    CommandValidation,
    Denormalizer,
    EventService,
    Storage,
    System,
    MarketDataApi,
    Concurrency,
    Query
}

public static class ErrorTypeExtensions
{
    public static string ToStringFast(this ErrorType value) => value switch
    {
        ErrorType.Undefined => nameof(ErrorType.Undefined),
        ErrorType.Command => nameof(ErrorType.Command),
        ErrorType.CommandValidation => nameof(ErrorType.CommandValidation),
        ErrorType.Denormalizer => nameof(ErrorType.Denormalizer),
        ErrorType.EventService => nameof(ErrorType.EventService),
        ErrorType.Storage => nameof(ErrorType.Storage),
        ErrorType.System => nameof(ErrorType.System),
        ErrorType.MarketDataApi => nameof(ErrorType.MarketDataApi),
        ErrorType.Concurrency => nameof(ErrorType.Concurrency),
        ErrorType.Query => nameof(ErrorType.Query),
        _ => value.ToString()
    };
}
