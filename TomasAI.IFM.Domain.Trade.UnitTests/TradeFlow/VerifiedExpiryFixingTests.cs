using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Trade.Futures.Position.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Tests.Support;
using F=TomasAI.IFM.Tests.Support.ExpiryFixingTestScenario;
namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;
public sealed class VerifiedExpiryFixingTests
{
    [Theory]
    [InlineData(7525,false,589.93)]
    [InlineData(7475,false,539.93)]
    [InlineData(7450,true,0)]
    [InlineData(6900,true,35.07)]
    public void Official_fixing_values_expiry_exactly_without_bid_ask_or_fills(int strike,bool call,double expected)
    {
        var acquired=F.Quote(F.Convention(strike,call));var q=acquired.Quote;
        VerifiedExpiryFixing.Verify(q,acquired.OriginalRecord);
        Assert.True(q.ExpiryFixing!.Covers(q,F.Boundary));Assert.Equal((decimal)expected,q.MarkPrice);
        Assert.Equal(0,q.Bid);Assert.Equal(0,q.Ask);Assert.Equal(0u,q.BidSize);Assert.Equal(0u,q.AskSize);Assert.False(q.HasBid);
        Assert.Equal(q,MessagePackBinarySerializer.Shared.Deserialize<StoredPositionClosingQuote>(MessagePackBinarySerializer.Shared.Serialize(q)));
    }
    [Theory]
    [InlineData(3,1,2,1,10252)] // preliminary actual settlement is not a fixing
    [InlineData(3,1,3,1,10252)] // even a final settlement is a different observation
    [InlineData(10,2,0,1,10252)]
    [InlineData(10,1,0,2,10252)]
    [InlineData(10,1,0,1,999)]
    public void Source_type_deletion_and_exact_underlying_are_verified_even_with_a_valid_archive_hash(int type,int action,int flags,int publisher,int instrument)
    {
        var raw=Encoding.UTF8.GetBytes(F.Header+"\n"+F.Row(type,action,flags,publisher,instrument)+"\n");
        var q=F.Quote(F.Convention()).Quote with{SourceArchiveSha256=Convert.ToHexString(SHA256.HashData(raw))};
        Assert.Throws<InvalidDataException>(()=>VerifiedExpiryFixing.Verify(q,raw));
    }
    [Fact]
    public void Settlement_revaluation_preserves_original_market_times_and_keeps_the_position_open()
    {
        var original=F.Position();var quotes=original.Legs.Select(l=>F.Quote(F.Convention(l.Strike!.Value)).Quote).ToArray();
        var result=StoredQuotePositionEndOfDayRecovery.Recover(original,original,F.Date,F.Boundary,quotes);
        Assert.True(result.Current.IsOpen);Assert.Empty(result.Current.ClosingFills);
        Assert.Equal(-42m,result.Seal.UnrealizedPnl);Assert.Equal(original.LastMarketObservationUtc,result.Seal.LastMarketObservationUtc);
        foreach(var leg in original.Legs){var marked=result.Seal.Legs.Single(l=>l.TradeLegId==leg.TradeLegId);Assert.Equal(leg.LastPriceAtUtc,marked.LastPriceAtUtc);Assert.Equal(leg.LastSourceSequence,marked.LastSourceSequence);Assert.Equal(leg.SignedQuantity,marked.SignedQuantity);}
        Assert.True(ClosingBookArchive.Qualifies(result.Seal,F.Boundary,TimeSpan.FromMinutes(15)));
        var rolled=original with{ValueDate=F.Date.AddDays(3),Phase=StrategyPositionPhase.MarkToMarket,
            AsOfUtc=F.Boundary.AddDays(3),DailyOpeningPnl=original.RealizedPnl+original.UnrealizedPnl};
        var late=StoredQuotePositionEndOfDayRecovery.Recover(original,rolled,F.Date,F.Boundary,quotes);
        Assert.Equal(-42m,late.Current.UnrealizedPnl);Assert.Equal(0,late.Current.DailyPnlPoints);
        Assert.Equal(rolled.LastMarketObservationUtc,late.Current.LastMarketObservationUtc);
        // A later session uses the same original expiry fixing, without inventing fresh quote timestamps.
        var date=F.Date.AddDays(3);var boundary=F.Boundary.AddDays(3);
        var later=result.Seal with{ValueDate=date,AsOfUtc=boundary,CloseBoundaryUtc=boundary,DailyOpeningPnl=-42};
        var laterQuotes=quotes.Select(q=>q with{ValueDate=date}).ToArray();
        var continued=StoredQuotePositionEndOfDayRecovery.Recover(later,later,date,boundary,laterQuotes,result.Seal);
        Assert.Equal(0,continued.Seal.DailyPnlPoints);Assert.Equal(original.LastMarketObservationUtc,continued.Seal.LastMarketObservationUtc);
    }
    [Fact]
    public void Residual_delivery_partial_expiry_and_inconsistent_fixings_cannot_pass_readiness()
    {
        var position=F.Position();var quotes=position.Legs.Select(l=>F.Quote(F.Convention(l.Strike!.Value)).Quote).ToArray();
        var unbalanced=position with{Legs=[position.Legs[0] with{SignedQuantity=-2},position.Legs[1]]};
        Assert.Throws<InvalidOperationException>(()=>VerifiedExpiryFixing.RequireOffsetDelivery(unbalanced,quotes,F.Boundary));
        Assert.Throws<InvalidOperationException>(()=>VerifiedExpiryFixing.RequireOffsetDelivery(position,[quotes[0]],F.Boundary));
        Assert.False(ClosingBookArchive.Qualifies(unbalanced with{EndOfDayClosingQuotes=quotes},F.Boundary,TimeSpan.FromMinutes(15)));
        var wrong=quotes[0] with{ExpiryFixing=quotes[0].ExpiryFixing! with{FixingPrice=6936}};
        Assert.Throws<InvalidDataException>(()=>VerifiedExpiryFixing.Verify(wrong,F.Quote(F.Convention()).OriginalRecord));
        Assert.False(quotes[0].ExpiryFixing!.Covers(quotes[0],F.Expiry.AddSeconds(-1)));
    }
}
