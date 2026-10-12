using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

public interface IMarketOutlookUpdateWriter
{
    MarketOutlookUpdateSubmission Submit(MarketOutlookUpdate update);
}
