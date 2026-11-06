//! Source integrity guard for the frozen managed backend and exact reviewed changes.

use sha2::{Digest, Sha256};
use std::path::Path;

#[test]
fn current_backend_source_and_build_inputs_remain_unchanged() {
    let baseline: serde_json::Value =
        serde_json::from_str(include_str!("../baseline/CurrentBackend.json")).unwrap();
    // Pin the committed backend inputs separately from the original baseline.
    // Future edits to either reviewed or untouched files still fail the guard.
    let reviewed: serde_json::Value =
        serde_json::from_str(include_str!("../baseline/ReviewedBackendChanges.json")).unwrap();
    let mut reviewed = reviewed["sourceFiles"].as_object().unwrap().clone();
    let repository = Path::new(env!("CARGO_MANIFEST_DIR")).parent().unwrap();
    for source in baseline["sourceFiles"].as_array().unwrap() {
        let relative = source["path"].as_str().unwrap();
        let text = std::fs::read_to_string(repository.join(relative)).unwrap();
        let normalized = text.trim_start_matches('\u{feff}').replace("\r\n", "\n");
        let actual = format!("{:x}", Sha256::digest(normalized.as_bytes()));
        let expected = reviewed.remove(relative);
        assert_eq!(
            actual,
            expected
                .as_ref()
                .unwrap_or(&source["sha256Utf8Lf"])
                .as_str()
                .unwrap(),
            "Current backend changed: {relative}"
        );
    }
    assert!(
        reviewed.is_empty(),
        "Reviewed paths must belong to the frozen baseline"
    );
    assert_eq!(baseline["apiMethods"].as_array().unwrap().len(), 71);
    assert_eq!(baseline["apiProperties"].as_array().unwrap().len(), 2);
}
