using System.Net;

namespace TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;

public class FinancialModelingPrepException : Exception
{
    public FinancialModelingPrepException(string message)
        : base(message)
    {
    }

    public FinancialModelingPrepException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class FinancialModelingPrepConfigurationException : FinancialModelingPrepException
{
    public FinancialModelingPrepConfigurationException(string message)
        : base(message)
    {
    }
}

public sealed class FinancialModelingPrepValidationException : FinancialModelingPrepException
{
    public FinancialModelingPrepValidationException(string message)
        : base(message)
    {
    }
}

public sealed class FinancialModelingPrepAuthenticationException : FinancialModelingPrepException
{
    public FinancialModelingPrepAuthenticationException(HttpStatusCode statusCode)
        : base($"FMP rejected the request credentials with HTTP status {(int)statusCode}.")
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}

public sealed class FinancialModelingPrepRateLimitException : FinancialModelingPrepException
{
    public FinancialModelingPrepRateLimitException(HttpStatusCode statusCode)
        : base($"FMP rate-limited the request with HTTP status {(int)statusCode} after bounded retries.")
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}

public sealed class FinancialModelingPrepUnavailableException : FinancialModelingPrepException
{
    public FinancialModelingPrepUnavailableException(string message)
        : base(message)
    {
    }

    public FinancialModelingPrepUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public class FinancialModelingPrepResponseException : FinancialModelingPrepException
{
    public FinancialModelingPrepResponseException(string message)
        : base(message)
    {
    }

    public FinancialModelingPrepResponseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class FinancialModelingPrepResponseTooLargeException : FinancialModelingPrepResponseException
{
    public FinancialModelingPrepResponseTooLargeException(int maximumBytes)
        : base($"The FMP response exceeded the configured {maximumBytes}-byte limit.")
    {
    }
}

public sealed class FinancialModelingPrepContractException : FinancialModelingPrepResponseException
{
    public FinancialModelingPrepContractException(string message)
        : base(message)
    {
    }

    public FinancialModelingPrepContractException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
