namespace TomasAI.IFM.Application.MarketData.UnitTests.Harness;

/// <summary>Integration tests must explicitly select an isolated broker, never the development broker.</summary>
internal static class ExplicitBrokerUrl
{
    internal static string Value
    {
        get
        {
            return Validate(Environment.GetEnvironmentVariable("IFM_MARKETDATA_TEST_NATS_URL"));
        }
    }
    internal static string Validate(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "nats"
            || uri.Host != "127.0.0.1" || uri.Port <= 1024 || uri.Port == 4222
            || uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidOperationException("Set IFM_MARKETDATA_TEST_NATS_URL to an explicitly isolated loopback broker on a non-development port.");
        return value!;
    }
}
