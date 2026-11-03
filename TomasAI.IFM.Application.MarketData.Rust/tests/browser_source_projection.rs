//! Compact browser facts extend the existing source by one untouched raw field;
//! background snapshots stay source-compatible and cannot masquerade as browser facts.
use ifm_market_data_native::reference::option_contract::{CoverageRow, OptionContract};
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
    let request: serde_json::Value = serde_json::from_slice(&browser.selected_model_request().unwrap()).unwrap();
    assert_eq!(request["contract"], source[0]);
    assert_eq!(request["version"], source[29]);
    assert_eq!(request["source"]["root"], source[2]);
    assert_eq!(request["source"]["underlying"], source[33]);
    assert_eq!(request["source"]["expiry"], source[8]);
    assert!(browser.matches(&OptionContract::from_command(source).unwrap()).unwrap());
    let mut stale = source.clone();
    stale[13] = serde_json::json!(1);
    stale[29] = serde_json::Value::Null;
    assert!(!browser.matches(&OptionContract::from_command(&stale).unwrap()).unwrap());
    let mut unversioned = source.clone();
    unversioned[29] = serde_json::Value::Null;
    assert!(CoverageRow::from_command(&unversioned).unwrap().selected_model_request().is_err());
    compact[24] = serde_json::json!(123);
    assert!(CoverageRow::from_command(&serde_json::json!(compact)).is_err());
    compact.push(serde_json::Value::Null);
    assert!(CoverageRow::from_command(&serde_json::json!(compact)).is_err());
}
