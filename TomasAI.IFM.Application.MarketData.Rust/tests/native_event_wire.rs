//! The existing C# production formatters are the independent event-wire oracle.
use base64::Engine;
use ifm_market_data_native::{
    aggregation::session::{Snapshot, VolumeQuality},
    dataset::{
        live::Registration,
        records::{Header, Quote, Trade},
        transport::Lane,
    },
    latest::Asset,
    messaging::{
        codec,
        events::{Identity, Instrument},
    },
};
use time::{Date, Month};

fn date() -> Date {
    Date::from_calendar_date(2026, Month::October, 27).unwrap()
}
fn registration(variant: i64) -> Registration {
    Registration {
        instrument: 42,
        publisher: if variant == 3 { 105 } else { 3 },
        contract: if variant == 3 {
            "VX / test"
        } else {
            "ES20261218"
        }
        .into(),
        source: "42".into(),
        asset: Asset::Futures,
        tick: Some(250000000),
        vwap: variant != 3,
        required: true,
    }
}
fn header(variant: i64) -> Header {
    Header {
        instrument: 42,
        publisher: if variant == 3 { 105 } else { 3 },
        flags: 4,
        event_ns: 1793116800000000001,
        received_ns: 1793116800000000099,
        sequence: u32::MAX,
        schema: 0,
    }
}
#[test]
fn existing_csharp_tick_events_are_byte_identical() {
    let rows: serde_json::Value =
        serde_json::from_str(include_str!("../qualification/EventWireBridge/events.json")).unwrap();
    for row in rows.as_array().unwrap() {
        let variant = row["variant"].as_i64().unwrap();
        let lane = if variant == 3 {
            Lane::FuturesXcbf
        } else {
            Lane::FuturesGlbx
        };
        let instrument = Instrument::new(
            registration(variant),
            lane,
            date(),
            date().previous_day().unwrap(),
        )
        .unwrap();
        let identity = Identity {
            event: [
                0x33, 0x22, 0x11, 0, 0x55, 0x44, 0x77, 0x66, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd,
                0xee, 0xff,
            ],
            command: [
                0xcc, 0xdd, 0xee, 0xff, 0xaa, 0xbb, 0x88, 0x99, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22,
                0x11, 0,
            ],
            observed: row["observed"].as_i64().unwrap(),
        };
        let price = match variant {
            0 => 5900250000000,
            1 => -1,
            2 => i64::MAX,
            _ => i64::MIN,
        };
        let value = match row["kind"].as_str().unwrap() {
            "trade" => instrument
                .trade(
                    Trade {
                        header: header(variant),
                        price,
                        size: u32::MAX,
                        action: 84,
                        side: 65,
                        dbn_flags: 255,
                        depth: 0,
                        delta_ns: 0,
                    },
                    identity,
                    65536,
                )
                .unwrap(),
            "quote" => instrument
                .quotes(
                    &vec![
                        Quote {
                            header: header(variant),
                            bid: price,
                            ask: i64::MAX,
                            bid_size: u32::MAX,
                            ask_size: 128,
                            bid_count: 127,
                            ask_count: 256
                        };
                        variant as usize + 1
                    ],
                    variant as u8 + 1,
                    identity,
                    65536,
                )
                .unwrap(),
            "stats" => instrument
                .statistics(
                    Snapshot {
                        value_date: date().previous_day().unwrap(),
                        open: 5900250000000,
                        high: 5901500000000,
                        low: 5800000000000,
                        sequence: u32::MAX,
                        event_ns: 1793116800000000001,
                        volume: 65536,
                        quality: match variant {
                            0 => VolumeQuality::Unknown,
                            1 => VolumeQuality::Bootstrapping,
                            2 => VolumeQuality::ObservedComplete,
                            _ => VolumeQuality::OfficialFinal,
                        },
                    },
                    identity,
                )
                .unwrap(),
            "market" | "vwap" => {
                use ifm_market_data_native::{
                    aggregation::vwap::{Checkpoint, Decimal, InvalidReason},
                    latest::{QuoteValue, Tick, TradeValue, market},
                };
                let ticks = identity.observed;
                let quote = Tick {
                    sequence: i64::from(u32::MAX),
                    event_ticks: ticks,
                    receive_ticks: ticks,
                    value: QuoteValue {
                        bid: Some(5900250000000),
                        ask: None,
                        bid_size: u32::MAX,
                        ask_size: 128,
                        bid_count: 127,
                        ask_count: 256,
                        local_received_ticks: ticks,
                    },
                };
                let trade = Tick {
                    sequence: i64::from(u32::MAX),
                    event_ticks: ticks,
                    receive_ticks: ticks,
                    value: market::Trade {
                        value: TradeValue {
                            price: 5900250000000,
                            size: u32::MAX,
                        },
                        action: 1,
                        side: 2,
                        conditions: 3,
                        epoch: u128::from_le_bytes(identity.event),
                        ordinal: 65536,
                    },
                };
                let snapshot = (variant != 3).then_some(market::Snapshot {
                    date: date(),
                    quote: (variant != 1).then_some(quote),
                    trade: (variant != 0).then_some(trade),
                });
                let checkpoint = (row["kind"] == "vwap").then_some(Checkpoint {
                    epoch: u128::from_le_bytes(identity.event),
                    ordinal: 65536,
                    sequence: i64::from(u32::MAX),
                    at: ticks,
                    price_volume: Decimal {
                        coefficient: 1234567890,
                        scale: 3,
                    },
                    volume: 65536,
                    eligible: 127,
                    rejected: 128,
                    last_price: Decimal {
                        coefficient: 5900250,
                        scale: 3,
                    },
                    valid: true,
                    replay_complete: true,
                    generation: u128::from_le_bytes(identity.command),
                    reason: match variant {
                        0 => InvalidReason::None,
                        1 => InvalidReason::DeliveryGap,
                        2 => InvalidReason::StreamEpochChanged,
                        _ => InvalidReason::UncorrelatableCorrection,
                    },
                });
                instrument
                    .market(snapshot, 2, checkpoint, None, identity)
                    .unwrap()
            }
            _ => panic!("Unexpected oracle kind"),
        };
        let mut actual = Vec::new();
        rmpv::encode::write_value(&mut actual, &value).unwrap();
        let expected = base64::engine::general_purpose::STANDARD
            .decode(row["content"].as_str().unwrap())
            .unwrap();
        let expected_value = rmpv::decode::read_value(&mut expected.as_slice()).unwrap();
        assert_eq!(value, expected_value, "{} variant {}", row["kind"], variant);
        assert_eq!(actual, expected, "wire {} variant {}", row["kind"], variant);
    }
}

#[test]
fn invalid_scalars_and_unqualified_event_inputs_fail_explicitly() {
    assert!(codec::timestamp(-621355968000000001).is_err());
    assert!(codec::timestamp(2534023008000000000).is_err());
    assert!(codec::decimal(1i128 << 96, 0).is_err());
    assert!(codec::decimal(1, 29).is_err());
    let instrument = Instrument::new(registration(0), Lane::FuturesGlbx, date(), date()).unwrap();
    let identity = Identity::fresh(0);
    assert!(instrument.quotes(&[], 1, identity, 1).is_err());
    assert!(
        instrument
            .quotes(
                &[Quote {
                    header: header(3),
                    bid: 1,
                    ask: 2,
                    bid_size: 1,
                    ask_size: 1,
                    bid_count: 1,
                    ask_count: 1
                }],
                1,
                identity,
                1
            )
            .is_err()
    );
    let mut invalid = registration(0);
    invalid.publisher = 0;
    assert!(Instrument::new(invalid, Lane::FuturesGlbx, date(), date()).is_err());
}
