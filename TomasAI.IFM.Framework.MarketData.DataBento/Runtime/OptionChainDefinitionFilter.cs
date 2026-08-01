namespace TomasAI.IFM.Framework.MarketData.DataBento;

internal static class OptionChainDefinitionFilter
{
    private const decimal PriceScale = 1_000_000_000m;

    internal static OptionChainDefinitions Create(
        string dataset,
        OptionChainDefinitionRequest request,
        ContractDetail? selectedUnderlying,
        IEnumerable<ContractDetail> details)
    {
        var instruments = new HashSet<InstrumentKey>();
        var rawSymbols = new HashSet<string>(StringComparer.Ordinal);
        var definitions = new List<OptionContractDefinition>();
        foreach (var detail in details
            .Where(detail => IsSelectedOption(detail, request.Rights)
                             && detail.MaturityDate == request.MaturityDate
                             && MatchesRequestedUnderlying(
                                 detail,
                                 request,
                                 selectedUnderlying))
            .OrderBy(detail => detail.StrikePrice)
            .ThenBy(detail => detail.ContractKind)
            .ThenBy(detail => detail.RawSymbol, StringComparer.Ordinal))
        {
            if (detail.StrikePrice is not { } strikePrice
                || instruments.Contains(detail.Instrument)
                || rawSymbols.Contains(detail.RawSymbol))
            {
                continue;
            }
            instruments.Add(detail.Instrument);
            rawSymbols.Add(detail.RawSymbol);
            definitions.Add(new OptionContractDefinition
            {
                Dataset = detail.Dataset,
                RawSymbol = detail.RawSymbol,
                Ticker = detail.Ticker,
                Underlying = detail.Underlying,
                Instrument = detail.Instrument,
                Right = detail.ContractKind == ContractKind.CallOption
                    ? OptionRightSelection.Call
                    : OptionRightSelection.Put,
                StrikePrice = strikePrice / PriceScale,
                MaturityDate = request.MaturityDate,
                ExpirationTimestampNanoseconds = detail.ExpirationTimestampNanoseconds,
                ActivationTimestampNanoseconds = detail.ActivationTimestampNanoseconds,
                MinimumPriceIncrement = detail.MinimumPriceIncrement,
                ContractMultiplier = detail.ContractMultiplier
            });
        }
        return new OptionChainDefinitions
        {
            Dataset = dataset,
            Underlying = request.Underlying,
            MaturityDate = request.MaturityDate,
            UniversePolicy = request.UniversePolicy,
            Rights = request.Rights,
            Contracts = Array.AsReadOnly(definitions.ToArray())
        };
    }

    private static bool IsSelectedOption(
        ContractDetail detail,
        OptionRightSelection rights)
    {
        var right = detail.ContractKind switch
        {
            ContractKind.CallOption => OptionRightSelection.Call,
            ContractKind.PutOption => OptionRightSelection.Put,
            _ => OptionRightSelection.None
        };
        return right != OptionRightSelection.None && (rights & right) != 0;
    }

    private static bool MatchesRequestedUnderlying(
        ContractDetail detail,
        OptionChainDefinitionRequest request,
        ContractDetail? selectedUnderlying)
    {
        if (request.UniversePolicy == OptionUniversePolicy.ParentOptionSymbol)
        {
            return true;
        }
        if (selectedUnderlying is not null)
        {
            return string.Equals(
                       detail.Underlying,
                       selectedUnderlying.RawSymbol,
                       StringComparison.Ordinal)
                   || detail.UnderlyingInstrumentId
                   == selectedUnderlying.Instrument.InstrumentId;
        }
        return string.Equals(detail.Underlying, request.Underlying, StringComparison.Ordinal)
               || detail.Underlying.StartsWith(request.Underlying, StringComparison.Ordinal)
               || string.Equals(detail.Ticker, request.Underlying, StringComparison.Ordinal);
    }
}
