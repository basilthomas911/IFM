using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.MarketData.Pricing;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
/// <summary>Stable copied business identity. It contains no financial model,
/// provider handle, dataset generation, clock, lease or mutable registry.</summary>
public static class NativePreparationIdentity
{
    /// <summary>Retries of one immutable preparation key address one native
    /// desired owner. A different workflow revision/input gets another owner.</summary>
    public static Guid For(CompositionPreparationKey key)
    {
        CompositionPreparationService.ValidateKey(key);
        var digest=SHA256.HashData(Encoding.UTF8.GetBytes("native-preparation/v1|"+key.WorkflowId.ToString("D")+"|"
            +key.InputRevision.ToString(CultureInfo.InvariantCulture)+"|"+key.InputSha256));
        return new Guid(digest.AsSpan(0,16));
    }
}
