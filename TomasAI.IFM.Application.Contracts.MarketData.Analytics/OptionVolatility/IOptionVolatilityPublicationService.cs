using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public interface IOptionVolatilityPublicationService
{
    Task PublishAsync(OptionIvPublication publication,
        CancellationToken cancellationToken = default);
}
