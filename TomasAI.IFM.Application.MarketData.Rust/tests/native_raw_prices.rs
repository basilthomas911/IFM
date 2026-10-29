//! Exact raw query units, missing-side and timestamp boundary parity with C#.
use base64::{Engine, engine::general_purpose::STANDARD};
use ifm_market_data_native::{
    dataset::live::RawPrices,
    latest::{QuoteValue, Tick, TradeValue},
    messaging::{codec, raw_prices},
};
#[test]
fn all_raw_source_boundaries_match_existing_csharp_public_snapshot_units() {
    let fixtures: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/NativeOwnershipBridge/raw-prices.json"
    ))
    .unwrap();
    for fixture in fixtures.as_array().unwrap() {
        let price = fixture["price"].as_i64().unwrap();
        let sides = fixture["sides"].as_u64().unwrap();
        let quantity = fixture["quantity"].as_u64().unwrap() as u32;
        let at = fixture["at"].as_i64().unwrap();
        let raw = RawPrices {
            generation: 77,
            trade: Some(Tick {
                sequence: i64::MAX,
                event_ticks: at,
                receive_ticks: at + 1,
                value: TradeValue {
                    price,
                    size: quantity,
                },
            }),
            quote: Some(Tick {
                sequence: i64::MAX,
                event_ticks: at,
                receive_ticks: at + 1,
                value: QuoteValue {
                    bid: (sides & 1 != 0).then_some(price),
                    ask: (sides & 2 != 0).then_some(price),
                    bid_size: quantity,
                    ask_size: quantity,
                    bid_count: u32::MAX,
                    ask_count: u32::MAX,
                    local_received_ticks: at + 2,
                },
            }),
        };
        let actual =
            raw_prices::value("ES20261218", fixture["date"].as_i64().unwrap() as i32, raw).unwrap();
        assert_eq!(
            codec::encode(&actual).unwrap(),
            STANDARD
                .decode(fixture["content"].as_str().unwrap())
                .unwrap(),
            "price {price}, sides {sides}, quantity {quantity}, at {at}"
        );
    }
    assert_eq!(fixtures.as_array().unwrap().len(), 112);
}
#[test]
fn absent_observations_and_invalid_identity_do_not_invent_price_data() {
    let raw = RawPrices {
        generation: 1,
        trade: None,
        quote: None,
    };
    let value = raw_prices::value("ES20261218", 739915, raw).unwrap();
    let fields = value.as_array().unwrap();
    assert!(fields[0].is_nil() && fields[1].is_nil());
    assert!(raw_prices::value("", 739915, raw).is_err());
    assert!(raw_prices::value("ES20261218", 3652059, raw).is_err());
    assert!(
        raw_prices::value(
            "ES20261218",
            739915,
            RawPrices {
                generation: 0,
                ..raw
            }
        )
        .is_err()
    );
}
