using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TomasAI.IFM.Domain.Portfolio.Workflow;

public static class PortfolioCanonicalHash
{
    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static string Compute<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var json = JsonSerializer.Serialize(value, Options);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
