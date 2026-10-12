using System.Globalization;
using System.Text;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.Domain.Portfolio.Shared.Validation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Workflow;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Query;

public static class PortfolioPageToken
{
    public static string EncodeInteger(int value) => Encode($"i:{value.ToString(CultureInfo.InvariantCulture)}");
    public static int DecodeInteger(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return 0;
        var value = Decode(token);
        return value.StartsWith("i:", StringComparison.Ordinal) && int.TryParse(value.AsSpan(2), CultureInfo.InvariantCulture, out var parsed) && parsed >= 0 ? parsed : throw new FormatException("Page token is invalid.");
    }
    public static string EncodeTimestamp(DateTime value) { Utc(value); return Encode($"t:{value.Ticks.ToString(CultureInfo.InvariantCulture)}"); }
    public static DateTime? DecodeTimestamp(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var value = Decode(token);
        return value.StartsWith("t:", StringComparison.Ordinal) && long.TryParse(value.AsSpan(2), CultureInfo.InvariantCulture, out var ticks) ? new DateTime(ticks, DateTimeKind.Utc) : throw new FormatException("Page token is invalid.");
    }
    static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    static string Decode(string value) { try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); } catch (Exception ex) when (ex is FormatException or ArgumentException) { throw new FormatException("Page token is invalid.", ex); } }
    static void Utc(DateTime value) { if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC.",nameof(value)); }
}
