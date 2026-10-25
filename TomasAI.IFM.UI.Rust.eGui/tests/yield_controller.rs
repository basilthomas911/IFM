#[test]
fn yield_filter_ranges_follow_the_supplied_api_date_including_leap_years() {
    use chrono::NaiveDate;
    use ifm_ui_egui::state::yield_curve::range;
    let supplied = NaiveDate::from_ymd_opt(2000, 2, 3).unwrap();
    let (start, end) = range("Current Month", supplied).unwrap();
    assert_eq!(start.to_string(), "2000-02-01");
    assert_eq!(end.to_string(), "2000-02-29");
    let (start, end) = range("1999", supplied).unwrap();
    assert_eq!(start.to_string(), "1999-01-01");
    assert_eq!(end.to_string(), "1999-12-31");
    assert!(range("Local Today", supplied).is_err());
}
