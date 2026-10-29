//! Unchanged C# store oracle, including replay/official precedence, per-field
//! reference dates, copied watermarks, exact ties and checked overflow atomicity.
use ifm_market_data_native::{
    dataset::records::{Header, Statistic},
    option_chain::statistics::{Failure, Seed, Statistics},
};
use serde_json::{Value, json};
use time::{Date, Month};
fn date(value: i64) -> Date {
    Date::from_calendar_date(
        (value / 10000) as i32,
        Month::try_from((value / 100 % 100) as u8).unwrap(),
        (value % 100) as u8,
    )
    .unwrap()
}
fn number(value: Option<Date>) -> Option<i32> {
    value.map(|date| date.year() * 10000 + date.month() as i32 * 100 + i32::from(date.day()))
}
fn snapshot(s: Statistics) -> Value {
    json!({"volume":s.volume,"open_interest":s.open_interest,"statistics_at":s.statistics_at,
    "official":s.official,"volume_date":number(s.volume_date),"open_interest_date":number(s.open_interest_date),
    "volume_at":s.volume_at,"open_interest_at":s.open_interest_at,"observed":s.observed,"last_event_ns":s.last_event_ns,
    "last_sequence":s.last_sequence,"replay_complete":s.replay_complete})
}
#[test]
fn every_option_statistics_transition_matches_current_csharp() {
    let cases: Vec<Value> =
        serde_json::from_str(include_str!("../baseline/OptionStatisticsOracle.json")).unwrap();
    assert_eq!(cases.len(), 1020);
    let mut states = [Statistics::default(); 2];
    let mut expected_overflows = 0;
    for (index, case) in cases.iter().enumerate() {
        let target = case["target"].as_u64().unwrap() as usize;
        let day = date(case["date"].as_i64().unwrap());
        let reference = date(case["reference"].as_i64().unwrap());
        let result = match case["op"].as_str().unwrap() {
            "volume" => states[target]
                .accumulate(
                    case["size"].as_u64().unwrap() as u32,
                    case["event_ns"].as_i64().unwrap(),
                    case["sequence"].as_u64().unwrap() as u32,
                    day,
                )
                .map(|_| ()),
            "complete" => {
                states[target].complete(day);
                Ok(())
            }
            "seed" => {
                states[target].seed(
                    Seed {
                        volume: case["volume"].as_i64(),
                        volume_date: Some(reference),
                        open_interest: case["interest"].as_i64(),
                        open_interest_date: Some(reference),
                    },
                    day,
                );
                Ok(())
            }
            "stats" => {
                states[target].update(
                    case["volume"].as_i64(),
                    case["interest"].as_i64(),
                    reference,
                    case["at"].as_i64().unwrap(),
                );
                Ok(())
            }
            "copy" => {
                states[target] = states[case["source"].as_u64().unwrap() as usize];
                Ok(())
            }
            "restore_observed" => {
                states[target].observed = case["observed"].as_i64().unwrap();
                states[target].last_event_ns = case["event_ns"].as_i64().unwrap();
                states[target].last_sequence = case["sequence"].as_u64().unwrap() as u32;
                Ok(())
            }
            "reset" => {
                states[target] = Statistics::default();
                Ok(())
            }
            other => panic!("Unknown frozen operation {other}"),
        };
        let failure = match result {
            Ok(()) => None,
            Err(Failure::VolumeOverflow) => {
                expected_overflows += 1;
                Some("OverflowException")
            }
            other => panic!("Unexpected error {other:?}"),
        };
        assert_eq!(failure, case["failure"].as_str(), "case {index}: {case}");
        assert_eq!(
            snapshot(states[target]),
            case["expected"],
            "case {index}: {case}"
        );
    }
    assert_eq!(expected_overflows, 2);
}
fn record(kind: u16, action: u8, quantity: i64, reference_ns: i64, event_ns: i64) -> Statistic {
    Statistic {
        header: Header {
            instrument: 43,
            publisher: 3,
            flags: 0,
            event_ns,
            received_ns: event_ns,
            sequence: 1,
            schema: 20,
        },
        kind,
        action,
        quantity,
        reference_ns,
        price: 0,
        channel: 0,
        flags: 0,
    }
}
#[test]
fn provider_filters_and_nanosecond_date_boundaries_are_exact() {
    let day = date(20261026);
    let reference = day
        .with_hms(0, 0, 0)
        .unwrap()
        .assume_utc()
        .unix_timestamp_nanos() as i64;
    let mut s = Statistics::default();
    for input in [
        record(6, 2, 10, reference, 0),
        record(6, 1, -1, reference, 0),
        record(9, 1, i64::MAX, reference, 0),
        record(8, 1, 10, reference, 0),
        record(6, 1, 10, reference + 86400000000000, 0),
    ] {
        assert!(!s.provider(&input, day).unwrap());
        assert_eq!(s, Statistics::default());
    }
    assert!(s.provider(&record(6, 1, 500, reference, 199), day).unwrap());
    assert_eq!(s.volume, Some(500));
    assert_eq!(s.volume_at, Some(1));
    assert!(s.official);
    assert!(
        s.provider(&record(9, 1, 100, reference - 1, 299), day)
            .unwrap()
    );
    assert_eq!(s.open_interest_date, Some(date(20261025)));
    assert_eq!(s.statistics_at, Some(2));
    let mut epoch = Statistics::default();
    epoch.provider(&record(6, 1, 10, -1, -1), day).unwrap();
    assert_eq!(epoch.volume_date, Some(date(19700101)));
    assert_eq!(epoch.volume_at, Some(0));
}

#[test]
fn native_book_replay_reset_and_official_statistics_share_logical_watermarks() {
    use ifm_market_data_native::{
        dataset::{
            book::{Book, Update},
            records::{Observation, Trade},
        },
        ffi::{live::NativeMarketEvent, publication::NativePublication},
        latest::Asset,
    };
    use std::{
        collections::BTreeMap,
        sync::{Arc, Mutex},
    };
    let day = date(20261026);
    let bank = Arc::new(Mutex::new(BTreeMap::new()));
    let mut saved = Statistics::default();
    saved.seed(
        Seed {
            volume: Some(1000),
            volume_date: Some(day),
            open_interest: Some(400),
            open_interest_date: Some(date(20261023)),
        },
        day,
    );
    bank.lock().unwrap().insert(43, saved);
    let make = |generation| {
        let mut book = Book::<()>::new(day, generation, 8).unwrap();
        book.register(43, "ES20261218C6000", Asset::Option, 8)
            .unwrap();
        book.bind_option_statistics(bank.clone()).unwrap();
        book
    };
    let mut book = make(1);
    let trade = |event_ns, sequence, size, replay| {
        Observation::Trade(Trade {
            header: Header {
                instrument: 43,
                publisher: 3,
                flags: if replay { 2 } else { 0 },
                event_ns,
                received_ns: event_ns,
                sequence,
                schema: 0,
            },
            price: 100_000_000_000,
            size,
            action: b'T',
            side: b'B',
            dbn_flags: 0,
            depth: 0,
            delta_ns: 0,
        })
    };
    book.apply(1, trade(100, 7, 4, true), 0).unwrap();
    book.apply(1, trade(100, 7, 99, true), 0).unwrap();
    assert!(
        book.reader("ES20261218C6000")
            .unwrap()
            .unwrap()
            .trade()
            .unwrap()
            .is_none()
    );
    let stats = book
        .publication_state(43, Update::Unchanged)
        .option_statistics
        .unwrap();
    assert_eq!(stats.observed, 4);
    assert_eq!(stats.volume, Some(1000));
    book.apply(1, trade(200, 8, 6, false), 0).unwrap();
    book.complete_option_replay(1, 43).unwrap();
    let before = book
        .publication_state(43, Update::Unchanged)
        .option_statistics
        .unwrap();
    assert_eq!(before.volume, Some(10));
    assert!(before.replay_complete);
    book.retire().unwrap();
    let mut replacement = make(2);
    replacement.apply(2, trade(100, 7, 4, true), 0).unwrap();
    replacement.apply(2, trade(200, 8, 6, true), 0).unwrap();
    assert_eq!(
        replacement
            .publication_state(43, Update::Unchanged)
            .option_statistics,
        Some(before)
    );
    replacement.apply(2, trade(300, 9, 5, true), 0).unwrap();
    let reference = day
        .with_hms(0, 0, 0)
        .unwrap()
        .assume_utc()
        .unix_timestamp_nanos() as i64;
    replacement
        .apply(
            2,
            Observation::Statistic(record(6, 1, 9000, reference, 400)),
            0,
        )
        .unwrap();
    replacement
        .apply(
            2,
            Observation::Statistic(record(9, 1, 600, reference - 86400000000000, 500)),
            0,
        )
        .unwrap();
    let state = replacement.publication_state(43, Update::Unchanged);
    let stats = state.option_statistics.unwrap();
    assert_eq!(stats.volume, Some(9000));
    assert!(stats.official);
    assert_eq!(stats.open_interest, Some(600));
    assert_eq!(stats.open_interest_date, Some(date(20261025)));
    assert_eq!(stats.observed, 15);
    let frame = NativePublication::from_state(
        NativeMarketEvent {
            size: 128,
            lane: 3,
            kind: 4,
            instrument: 43,
            ..Default::default()
        },
        state,
    );
    assert_eq!(frame.flags & 32, 32);
    assert_eq!(frame.fields[53], 9000);
    assert_eq!(frame.fields[54], 600);
    assert_eq!(frame.fields[56], 20261026);
    assert_eq!(frame.fields[57], 20261025);
    assert_eq!(frame.fields[60], 15);
    assert_eq!(frame.fields[61], 300);
    assert_eq!(frame.fields[62], 9);
    assert_eq!(frame.fields[63], 0);
}
