using System.Net;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

public class FinancialModelingPrepException : Exception
{
    /// <summary>Initializes a new FinancialModelingPrepException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    public FinancialModelingPrepException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new FinancialModelingPrepException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    /// <param name="innerException">The underlying exception that caused this failure.</param>
    public FinancialModelingPrepException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class FinancialModelingPrepConfigurationException : FinancialModelingPrepException
{
    /// <summary>Initializes a new FinancialModelingPrepConfigurationException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    public FinancialModelingPrepConfigurationException(string message)
        : base(message)
    {
    }
}

public sealed class FinancialModelingPrepValidationException : FinancialModelingPrepException
{
    /// <summary>Initializes a new FinancialModelingPrepValidationException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    public FinancialModelingPrepValidationException(string message)
        : base(message)
    {
    }
}

public sealed class FinancialModelingPrepAuthenticationException : FinancialModelingPrepException
{
    /// <summary>Initializes a new FinancialModelingPrepAuthenticationException instance.</summary>
    /// <param name="statusCode">The HTTP response status code.</param>
    public FinancialModelingPrepAuthenticationException(HttpStatusCode statusCode)
        : base($"FMP rejected the request credentials with HTTP status {(int)statusCode}.")
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}

public sealed class FinancialModelingPrepRateLimitException : FinancialModelingPrepException
{
    /// <summary>Initializes a new FinancialModelingPrepRateLimitException instance.</summary>
    /// <param name="statusCode">The HTTP response status code.</param>
    public FinancialModelingPrepRateLimitException(HttpStatusCode statusCode)
        : base($"FMP rate-limited the request with HTTP status {(int)statusCode} after bounded retries.")
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}

public sealed class FinancialModelingPrepUnavailableException : FinancialModelingPrepException
{
    /// <summary>Initializes a new FinancialModelingPrepUnavailableException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    public FinancialModelingPrepUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new FinancialModelingPrepUnavailableException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    /// <param name="innerException">The underlying exception that caused this failure.</param>
    public FinancialModelingPrepUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public class FinancialModelingPrepResponseException : FinancialModelingPrepException
{
    /// <summary>Initializes a new FinancialModelingPrepResponseException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    public FinancialModelingPrepResponseException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new FinancialModelingPrepResponseException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    /// <param name="innerException">The underlying exception that caused this failure.</param>
    public FinancialModelingPrepResponseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class FinancialModelingPrepResponseTooLargeException : FinancialModelingPrepResponseException
{
    /// <summary>Initializes a new FinancialModelingPrepResponseTooLargeException instance.</summary>
    /// <param name="maximumBytes">The maximum permitted response size in bytes.</param>
    public FinancialModelingPrepResponseTooLargeException(int maximumBytes)
        : base($"The FMP response exceeded the configured {maximumBytes}-byte limit.")
    {
    }
}

public sealed class FinancialModelingPrepContractException : FinancialModelingPrepResponseException
{
    /// <summary>Initializes a new FinancialModelingPrepContractException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    public FinancialModelingPrepContractException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new FinancialModelingPrepContractException instance.</summary>
    /// <param name="message">The diagnostic message describing the failure.</param>
    /// <param name="innerException">The underlying exception that caused this failure.</param>
    public FinancialModelingPrepContractException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
