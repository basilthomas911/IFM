using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public interface IInstrumentDefinitionProvider
{
    IAsyncEnumerable<ExactInstrumentDefinition> ReadLatestAsync(string dataset, CancellationToken cancellationToken = default);
}
