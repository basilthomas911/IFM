//! Differential checks against unchanged production C# framework functions.

use ifm_market_data_native::{
    option_chain::snapshot::{Publication, SnapshotStore},
    reference::treasury,
};

fn fixture() -> serde_json::Value {
    serde_json::from_str(include_str!("../baseline/FrameworkOracle.json")).unwrap()
}

#[test]
fn treasury_tenor_boundaries_match_the_current_csharp_implementation() {
    for row in fixture()["Tenors"].as_array().unwrap() {
        assert_eq!(
            treasury::select_tenor(row["Days"].as_i64().unwrap() as i32).map(u64::from),
            row["Tenor"].as_u64()
        );
    }
}

#[test]
fn treasury_rate_bits_match_the_current_csharp_implementation() {
    for row in fixture()["Treasury"].as_array().unwrap() {
        let value =
            treasury::continuous_rate(f64::from_bits(row["HalfYieldBits"].as_u64().unwrap()));
        if let Some(bits) = row["ContinuousRateBits"].as_u64() {
            assert_eq!(
                value.unwrap().to_bits(),
                bits,
                "Unexplained numerical difference for CMT percent {}",
                row["RatePercent"]
            );
        } else {
            assert_eq!(value.unwrap_err(), row["Error"].as_str().unwrap());
        }
    }
}

#[test]
fn bounded_snapshot_generation_and_publication_results_match_csharp() {
    let store = SnapshotStore::new(1).unwrap();
    let mut steps = Vec::new();
    let mut check = |name: &str, value: serde_json::Value| {
        steps.push(serde_json::json!({"Name":name,"Value":value}));
    };
    let publish = |generation, scope, version, at, value: &str| {
        store
            .publish(
                generation,
                scope,
                Publication {
                    version,
                    published_at_utc_ticks: at,
                    snapshot: value.to_owned(),
                },
            )
            .unwrap()
    };
    let read = |generation, scope| {
        store
            .read(generation, scope)
            .unwrap()
            .map(|value| value.snapshot.clone())
    };
    check("before-admit", serde_json::json!(read(1, "a")));
    store.admit(1).unwrap();
    check(
        "publish-first",
        serde_json::json!(publish(1, "a", 1, 100, "first")),
    );
    check(
        "duplicate-version",
        serde_json::json!(publish(1, "a", 1, 101, "duplicate")),
    );
    check(
        "capacity",
        serde_json::json!(publish(1, "b", 1, 100, "overflow")),
    );
    check(
        "older-time",
        serde_json::json!(publish(1, "a", 2, 99, "stale")),
    );
    check(
        "publish-newer",
        serde_json::json!(publish(1, "a", 2, 101, "second")),
    );
    check("latest", serde_json::json!(read(1, "a")));
    store.admit(2).unwrap();
    check("old-read", serde_json::json!(read(1, "a")));
    check(
        "old-publish",
        serde_json::json!(publish(1, "a", 3, 102, "old")),
    );
    check(
        "replacement-publish",
        serde_json::json!(publish(2, "a", 1, 100, "replacement")),
    );
    store.fence(1).unwrap();
    check(
        "old-fence-preserves-replacement",
        serde_json::json!(read(2, "a")),
    );
    store.remove(1, "a").unwrap();
    check(
        "old-remove-preserves-replacement",
        serde_json::json!(read(2, "a")),
    );
    store.fence(2).unwrap();
    check("fenced-read", serde_json::json!(read(2, "a")));
    assert_eq!(serde_json::json!(steps), fixture()["SnapshotStore"]);
}
