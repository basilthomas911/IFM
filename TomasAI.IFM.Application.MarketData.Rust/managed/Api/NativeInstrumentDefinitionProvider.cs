using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using TomasAI.IFM.Application.MarketData.Rust.Delivery;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Shared.MarketData.Definitions;
namespace TomasAI.IFM.Application.MarketData.Rust.Api;
/// <summary>Full definition-schema source with bounded native line streaming and
/// native parsing. Application snapshot/storage policy remains outside the driver.</summary>
public sealed class NativeInstrumentDefinitionProvider : IInstrumentDefinitionProvider
{
    readonly NativeMarketDataRuntime runtime;readonly TimeSpan budget;
    /// <summary>Uses a configured native history owner and one caller budget,
    /// including every source row and downstream consumer backpressure.</summary>
    public NativeInstrumentDefinitionProvider(NativeMarketDataRuntime runtime,TimeSpan budget)
    {this.runtime=runtime??throw new ArgumentNullException(nameof(runtime));if(budget<=TimeSpan.Zero||budget>TimeSpan.FromMinutes(30))throw new ArgumentOutOfRangeException(nameof(budget));this.budget=budget;}
    /// <inheritdoc/>
    public async IAsyncEnumerable<ExactInstrumentDefinition> ReadLatestAsync(string dataset,[EnumeratorCancellation]CancellationToken cancellationToken=default)
    {
        await using var reader=runtime.OpenDefinitions(dataset,budget,cancellationToken);var bytes=ArrayPool<byte>.Shared.Rent(65536);
        try{while(true){var length=await reader.ReadNextAsync(bytes,cancellationToken).ConfigureAwait(false);if(length<0)break;yield return runtime.ProjectDefinition(dataset,Encoding.UTF8.GetString(bytes,0,length));}}
        finally{ArrayPool<byte>.Shared.Return(bytes,true);}
    }
}
