using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Pricing;
namespace TomasAI.IFM.Application.MarketData.UnitTests;
public sealed class OptionChainAtmIvEstimatorTests
{
    [Fact]
    public async Task Selection_only_snapshot_bootstraps_expiry_IV_without_full_Greeks()
    {
        var snapshot = await OptionChainCacheTests.Snapshot(); var row = snapshot.Instruments.Single();
        var quote = row.Instrument.Quote!;
        var selection = new OptionSelectionValue(10, .5, .24, row.Instrument.Underlying!, quote,
            snapshot.EvaluatedAtUtc, snapshot.EvaluatedAtUtc, snapshot.EvaluatedAtUtc.AddSeconds(1), "test", "test");
        var call = row with { Valuation = null, Instrument = row.Instrument with { IsCall = true, Strike = 5000, Selection = selection } };
        var put = call with { Instrument = call.Instrument with { ContractId = "put", IsCall = false, Selection = selection with { ImpliedVolatility = .26 } } };
        var chain = snapshot with { Instruments = [call, put] };
        Assert.Equal(.25, OptionChainAtmIvEstimator.Read(chain, 5000)!.Value, 10);
        Assert.Null(OptionChainAtmIvEstimator.Read(chain with { Instruments = [call with { Instrument = call.Instrument with { Quote = null } }] }, 5000));
        Assert.Null(OptionChainAtmIvEstimator.Read(chain with { Instruments = [call with { Instrument = call.Instrument with { Selection = selection with { ValidUntilUtc = snapshot.EvaluatedAtUtc } } }] }, 5000));
    }

    [Fact]
    public async Task Full_valuation_remains_supported_and_far_strikes_do_not_seed_IV()
    {
        var snapshot = await OptionChainCacheTests.Snapshot(); var row = snapshot.Instruments.Single();
        snapshot = snapshot with { Instruments = [row with { Instrument = row.Instrument with { Strike = 5000 } }] };
        Assert.Equal(row.Valuation!.ImpliedVolatility, OptionChainAtmIvEstimator.Read(snapshot, 5000));
        Assert.Null(OptionChainAtmIvEstimator.Read(snapshot, 6000));
    }
}
