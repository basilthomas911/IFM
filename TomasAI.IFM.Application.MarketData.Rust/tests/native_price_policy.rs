//! Native application price policy covers current source selection and failures.
use ifm_market_data_native::{
    dataset::live::RawPrices,
    latest::{QuoteValue, Tick, TradeValue, price_policy},
    reference::decimal,
};
fn data() -> RawPrices {
    RawPrices {
        generation: 1,
        trade: Some(Tick {
            sequence: 1,
            event_ticks: 100,
            receive_ticks: 100,
            value: TradeValue {
                price: 123_000_000_000,
                size: 1,
            },
        }),
        quote: Some(Tick {
            sequence: 2,
            event_ticks: 200,
            receive_ticks: 200,
            value: QuoteValue {
                bid: Some(100_000_000_000),
                ask: Some(101_000_000_000),
                bid_size: 1,
                ask_size: 1,
                bid_count: 1,
                ask_count: 1,
                local_received_ticks: 200,
            },
        }),
    }
}
fn equal(
    actual: Option<ifm_market_data_native::option_chain::qualification::Decimal>,
    expected: &str,
) {
    assert_eq!(
        actual.unwrap().compare(decimal::parse(expected).unwrap()),
        Some(std::cmp::Ordering::Equal)
    );
}
#[test]
fn fresh_futures_trade_precedes_quote_but_options_always_use_quote() {
    equal(
        price_policy::calculate(data(), false, 200, 1000).unwrap(),
        "123",
    );
    equal(
        price_policy::calculate(data(), true, 200, 1000).unwrap(),
        "100.5",
    );
    equal(
        price_policy::calculate(data(), false, 200, 99).unwrap(),
        "100.5",
    );
    assert!(price_policy::calculate(data(), false, 200, 0).is_err());
    assert!(
        price_policy::calculate(data(), false, 1201, 1000)
            .unwrap()
            .is_none()
    );
    assert!(
        price_policy::calculate(data(), true, 199, 1000)
            .unwrap()
            .is_none()
    );
}
#[test]
fn crossed_option_is_typed_error_and_crossed_futures_are_unavailable() {
    let mut raw = data();
    raw.trade = None;
    raw.quote.as_mut().unwrap().value.bid = Some(102_000_000_000);
    assert_eq!(
        price_policy::calculate(raw, true, 200, 1000).unwrap_err(),
        "InvalidFuturesOptionQuote"
    );
    assert!(
        price_policy::calculate(raw, false, 200, 1000)
            .unwrap()
            .is_none()
    );
}
