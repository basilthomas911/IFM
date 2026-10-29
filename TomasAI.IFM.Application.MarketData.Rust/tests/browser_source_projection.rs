//! Compact browser facts extend the existing source by one untouched raw field;
//! background snapshots stay source-compatible and cannot masquerade as browser facts.
use ifm_market_data_native::reference::option_contract::CoverageRow;
#[test]
fn browser_projection_preserves_legacy_bits_and_type_without_inference() {
    let fixture: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/reviewed-options.json"
    ))
    .unwrap();
    let source = &fixture[0]["command"];
    let fields = [
        0, 2, 8, 9, 11, 12, 13, 14, 15, 16, 19, 21, 23, 24, 29, 33, 34, 35, 36, 37, 40, 42, 43, 44,
    ];
    let mut compact = fields
        .into_iter()
        .map(|i| source[i].clone())
        .collect::<Vec<_>>();
    let background = CoverageRow::from_command(&serde_json::json!(compact)).unwrap();
    assert!(background.browser_row().is_err());
    compact.push(source[10].clone());
    let browser = CoverageRow::from_command(&serde_json::json!(compact)).unwrap();
    let row = browser.browser_row().unwrap();
    assert_eq!(row.contract, source[0].as_str().unwrap());
    assert_eq!(row.kind, source[10].as_str().unwrap());
    assert_eq!(
        row.strike.to_bits(),
        u64::from_str_radix(source[9].as_str().unwrap(), 16).unwrap()
    );
    assert_eq!(browser.digest(), background.digest());
    compact[24] = serde_json::json!(123);
    assert!(CoverageRow::from_command(&serde_json::json!(compact)).is_err());
    compact.push(serde_json::Value::Null);
    assert!(CoverageRow::from_command(&serde_json::json!(compact)).is_err());
}
