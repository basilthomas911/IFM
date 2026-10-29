//! Exhaustive comparison to unchanged production C# functions, not copied expectations.

use ifm_market_data_native::aggregation::normalization;

#[test]
fn all_action_side_and_flag_bytes_match_csharp_exactly() {
    let fixture: serde_json::Value =
        serde_json::from_str(include_str!("../baseline/NormalizationOracle.json")).unwrap();
    let actions = fixture["Actions"].as_array().unwrap();
    let sides = fixture["Sides"].as_array().unwrap();
    let flags = fixture["ConditionsHeaderMajor"].as_array().unwrap();
    assert_eq!((actions.len(), sides.len(), flags.len()), (256, 256, 65536));
    for byte in 0..=255_u8 {
        assert_eq!(
            normalization::action(byte) as u64,
            actions[byte as usize].as_u64().unwrap(),
            "action byte={byte}"
        );
        assert_eq!(
            normalization::side(byte) as u64,
            sides[byte as usize].as_u64().unwrap(),
            "side byte={byte}"
        );
        for dbn in 0..=255_u8 {
            assert_eq!(
                normalization::conditions(byte, dbn) as u64,
                flags[byte as usize * 256 + dbn as usize].as_u64().unwrap(),
                "header={byte}; dbn={dbn}"
            );
        }
    }
}
