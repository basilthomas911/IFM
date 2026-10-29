//! Native business time cannot silently fall back to the workstation calendar.
use ifm_market_data_native::runtime::clock::{Clock, Failure};
#[test]
fn controlled_clock_accepts_api_updates_and_rejects_invalid_ranges_atomically() {
    let clock = Clock::new(1234, false).unwrap();
    assert_eq!(clock.utc_ticks().unwrap(), 1234);
    clock.update(-100, false).unwrap();
    assert_eq!(clock.utc_ticks().unwrap(), -100);
    assert_eq!(clock.update(i64::MAX, false), Err(Failure::OutOfRange));
    assert_eq!(clock.utc_ticks().unwrap(), -100);
    assert!(matches!(
        Clock::new(i64::MIN, true),
        Err(Failure::OutOfRange)
    ));
    clock.update(2534023007999999999, false).unwrap();
    assert_eq!(clock.utc_ticks().unwrap(), 2534023007999999999);
}
#[test]
fn advancing_anchor_uses_elapsed_time_and_can_return_to_explicit_control() {
    let clock = Clock::new(5000, true).unwrap();
    let first = clock.utc_ticks().unwrap();
    let next = clock.utc_ticks().unwrap();
    assert!(first >= 5000 && next >= first);
    clock.update(42, false).unwrap();
    for _ in 0..1000 {
        assert_eq!(clock.utc_ticks().unwrap(), 42);
    }
}
