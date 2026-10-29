//! Full cache acceptance/publication transitions against unchanged private C# cache.
use ifm_market_data_native::latest::{
    QuoteValue, Tick, TradeValue,
    market::{Market, Trade},
};
use serde_json::Value;
#[test]
fn market_price_cache_matches_tick_suppression_source_continuity_and_date_changes() {
    let cases: Vec<Value> =
        serde_json::from_str(include_str!("../baseline/MarketPriceOracle.json")).unwrap();
    let mut market = Market::default();
    for (index, c) in cases.iter().enumerate() {
        if c["reset"].as_bool().unwrap() {
            market = Market::new(c["tick"].as_i64());
        }
        let s = c["date"].as_str().unwrap();
        let date = time::Date::from_calendar_date(
            s[..4].parse().unwrap(),
            time::Month::try_from(s[5..7].parse::<u8>().unwrap()).unwrap(),
            s[8..].parse().unwrap(),
        )
        .unwrap();
        let seq = c["sequence"].as_i64().unwrap();
        let at = c["timestamp"].as_i64().unwrap();
        let price = c["price"].as_i64().unwrap();
        let (accepted, changed) = if c["quote"].as_bool().unwrap() {
            let changed = market.quote(
                date,
                Tick {
                    sequence: seq,
                    event_ticks: at,
                    receive_ticks: at,
                    value: QuoteValue {
                        bid: Some(price),
                        ask: Some(price + 250000000),
                        bid_size: 5,
                        ask_size: 6,
                        bid_count: 1,
                        ask_count: 2,
                        local_received_ticks: 0,
                    },
                },
            );
            (changed, changed)
        } else {
            market.trade(
                date,
                Tick {
                    sequence: seq,
                    event_ticks: at,
                    receive_ticks: at,
                    value: Trade {
                        value: TradeValue { price, size: 7 },
                        action: 0,
                        side: 0,
                        conditions: 0,
                        epoch: 0,
                        ordinal: 0,
                    },
                },
            )
        };
        assert_eq!(
            (accepted, changed),
            (
                c["accepted"].as_bool().unwrap(),
                c["changed"].as_bool().unwrap()
            ),
            "case {index}"
        );
        let s = market.snapshot().unwrap();
        assert_eq!(s.date.to_string(), c["snapshotDate"]);
        for (name, value) in [
            ("quoteSequence", s.quote.map(|q| q.sequence)),
            ("quoteTime", s.quote.map(|q| q.event_ticks)),
            ("quotePrice", s.quote.and_then(|q| q.value.bid)),
            ("tradeSequence", s.trade.map(|t| t.sequence)),
            ("tradeTime", s.trade.map(|t| t.event_ticks)),
            ("tradePrice", s.trade.map(|t| t.value.value.price)),
        ] {
            assert_eq!(value, c[name].as_i64(), "case {index} {name}");
        }
    }
}
