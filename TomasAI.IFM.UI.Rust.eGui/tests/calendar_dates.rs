use chrono::{NaiveDate, Timelike};
use ifm_ui_egui::state::calendar::{eastern_midnight, instant};
#[test]
fn calendar_days_use_eastern_dst_and_preserve_utc_event_precision() {
    let summer = eastern_midnight(NaiveDate::from_ymd_opt(2026, 7, 1).unwrap()).unwrap();
    let winter = eastern_midnight(NaiveDate::from_ymd_opt(2026, 1, 1).unwrap()).unwrap();
    assert_eq!(summer.hour(), 4);
    assert_eq!(winter.hour(), 5);
    assert_eq!(
        instant("2026-10-26T12:30:00.1234567Z")
            .unwrap()
            .timestamp_subsec_nanos(),
        123456700
    );
    assert!(instant("2026-10-26T08:30:00-04:00").is_err());
    assert!(instant("2026-10-26T12:30:00.123456789Z").is_err());
    assert!(instant("2026-10-26").is_err());
}
