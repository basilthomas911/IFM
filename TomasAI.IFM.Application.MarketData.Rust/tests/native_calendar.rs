//! Independent actual C# calendar results, including rollover/DST boundaries.
use ifm_market_data_native::reference::calendar::{Calendar, Zone};
use time::{Date, Month, Weekday};
fn date(year: i32, month: Month, day: u8) -> Date {
    Date::from_calendar_date(year, month, day).unwrap()
}
fn dates() -> Vec<Date> {
    let mut at = date(2025, Month::January, 1);
    let end = date(2027, Month::December, 31);
    let mut dates = Vec::new();
    while at <= end {
        if !matches!(at.weekday(), Weekday::Saturday | Weekday::Sunday) {
            dates.push(at);
        }
        at = at.next_day().unwrap();
    }
    dates
}
fn calendar(zone: &str) -> Calendar {
    Calendar::new(
        "fixture/v1".into(),
        zone,
        date(2025, Month::January, 1),
        date(2027, Month::December, 31),
        648_000_000_000,
        dates(),
    )
    .unwrap()
}
#[test]
fn all_csharp_timezone_rollover_and_dst_count_fixtures_match() {
    let rows: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/CalendarBridge/calendars.json"
    ))
    .unwrap();
    for row in rows.as_array().unwrap() {
        let name = row["zone"].as_str().unwrap();
        let at = row["at"].as_i64().unwrap();
        let expiry = row["expiry"].as_i64().unwrap();
        assert_eq!(
            Zone::new(name)
                .unwrap()
                .value_date(at, 648_000_000_000)
                .unwrap()
                .to_julian_day()
                - 1721426,
            row["valueDate"].as_i64().unwrap() as i32,
            "{name} {at}"
        );
        assert_eq!(
            calendar(name).count("fixture/v1", at, expiry).unwrap(),
            row["count"].as_i64().unwrap() as i32,
            "{name} {at}"
        );
    }
    assert_eq!(rows.as_array().unwrap().len(), 1728);
}
#[test]
fn all_exported_windows_aliases_resolve_without_os_callbacks() {
    let aliases: std::collections::BTreeMap<String, String> = serde_json::from_str(include_str!(
        "../qualification/CalendarBridge/windows-zones.json"
    ))
    .unwrap();
    for (windows, iana) in aliases {
        let a = Zone::new(&windows).unwrap();
        let b = Zone::new(&iana).unwrap();
        for at in [0, 17931168000000000, 17935128000000000] {
            assert_eq!(
                a.value_date(at, 648_000_000_000),
                b.value_date(at, 648_000_000_000),
                "{windows}"
            );
        }
    }
    assert!(!Zone::version().is_empty());
}
#[test]
fn invalid_calendar_coverage_and_unknown_zone_do_not_fall_back_to_local_dates() {
    assert!(Zone::new("Missing/Zone").is_err());
    assert!(Zone::new("").is_err());
    let calendar = calendar("America/Chicago");
    let at = 17931168000000000;
    let expiry = 17987328000000000;
    assert!(calendar.count("wrong/version", at, expiry).is_err());
    assert!(calendar.count("fixture/v1", expiry, expiry).is_err());
    assert!(
        Zone::new("UTC")
            .unwrap()
            .value_date(at, 864_000_000_000)
            .is_err()
    );
    let from = date(2026, Month::January, 1);
    let until = date(2026, Month::December, 31);
    for values in [
        vec![from, from],
        vec![until, from],
        vec![date(2025, Month::December, 31)],
    ] {
        assert!(Calendar::new("v".into(), "UTC", from, until, 0, values).is_err());
    }
    assert!(Calendar::new("".into(), "UTC", from, until, 0, vec![]).is_err());
    assert!(Calendar::new("v".into(), "UTC", until, from, 0, vec![]).is_err());
    assert!(Calendar::new("v".into(), "UTC", from, until, 0, vec![from; 2001]).is_err());
}
