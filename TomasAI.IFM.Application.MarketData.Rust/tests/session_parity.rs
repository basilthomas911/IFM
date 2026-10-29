//! Ordered transition oracle from the unchanged C# accumulator.
use ifm_market_data_native::{
    aggregation::session::{Failure, Sessions},
    dataset::records::{Header, Statistic, Trade},
};
use serde_json::Value;
fn day(text: &str) -> time::Date {
    let pieces: Vec<i32> = text.split('-').map(|s| s.parse().unwrap()).collect();
    time::Date::from_calendar_date(
        pieces[0],
        time::Month::try_from(pieces[1] as u8).unwrap(),
        pieces[2] as u8,
    )
    .unwrap()
}
#[test]
fn session_accumulator_matches_every_csharp_transition_exactly() {
    let cases: Vec<Value> =
        serde_json::from_str(include_str!("../baseline/SessionOracle.json")).unwrap();
    let date = day("2026-10-26");
    let mut sessions = Sessions::new(4).unwrap();
    for (index, c) in cases.iter().enumerate() {
        let u = |name: &str| c[name].as_u64().unwrap();
        let i = |name: &str| c[name].as_i64().unwrap();
        let header = Header {
            instrument: 42,
            publisher: 7,
            flags: u("flags") as u8,
            event_ns: (u("sequence") * 1000) as i64,
            received_ns: 0,
            sequence: u("sequence") as u32,
            schema: 0,
        };
        let (changed, snapshot) = match c["operation"].as_str().unwrap() {
            "trade" => sessions
                .trade(
                    date,
                    &Trade {
                        header,
                        price: i("price"),
                        size: u("size") as u32,
                        action: b'T',
                        side: b'N',
                        dbn_flags: 0,
                        depth: 0,
                        delta_ns: 0,
                    },
                    c["replay"].as_bool().unwrap(),
                )
                .unwrap(),
            "statistic" => sessions
                .statistic(
                    date,
                    &Statistic {
                        header,
                        price: i("price"),
                        quantity: i("quantity"),
                        reference_ns: i("reference"),
                        kind: u("kind") as u16,
                        channel: 0,
                        action: u("action") as u8,
                        flags: 0,
                    },
                )
                .unwrap(),
            "complete" => (true, sessions.complete_replay(date).unwrap()),
            _ => panic!("unknown oracle operation"),
        };
        assert_eq!(
            changed,
            c["changed"].as_bool().unwrap(),
            "transition {index}"
        );
        assert_eq!(snapshot.value_date, day(c["date"].as_str().unwrap()));
        assert_eq!(
            [
                snapshot.open,
                snapshot.high,
                snapshot.low,
                snapshot.event_ns,
                snapshot.volume
            ],
            [i("open"), i("high"), i("low"), i("eventNs"), i("volume")],
            "transition {index}"
        );
        assert_eq!(snapshot.sequence, u("source") as u32);
        assert_eq!(snapshot.quality as u64, u("quality"));
        assert_eq!(snapshot.has_prices(), c["prices"].as_bool().unwrap());
        assert_eq!(snapshot.has_volume(), c["hasVolume"].as_bool().unwrap());
        assert_eq!(snapshot.has_any(), c["any"].as_bool().unwrap());
    }
}
#[test]
fn session_dates_are_bounded_and_reset_releases_old_state() {
    assert!(matches!(Sessions::new(0), Err(Failure::Capacity)));
    let mut sessions = Sessions::new(1).unwrap();
    let date = day("2026-10-26");
    sessions.complete_replay(date).unwrap();
    assert_eq!(
        sessions.complete_replay(day("2026-10-27")),
        Err(Failure::Capacity)
    );
    sessions.reset();
    assert!(sessions.read(date).is_none());
    sessions.complete_replay(day("2026-10-27")).unwrap();
}
