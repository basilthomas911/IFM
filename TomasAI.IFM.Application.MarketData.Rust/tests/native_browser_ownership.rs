//! Explicit native browser demand survives idle UI time and remains bounded.
use ifm_market_data_native::option_chain::browser::{Command, Directory};
use serde_json::json;
use std::time::Duration;
use tokio::time::{Instant, advance};
fn command(owner: &str, required: &[&str]) -> Command {
    Command::parse(&json!({"contract":"ES20261218","root":"ES","expiry":739929,"providers":["ew3","EW3"],"owner":owner,"required":required,"multiplier":2.5,"wing":"25","release":false})).unwrap()
}
#[tokio::test(start_paused = true)]
async fn idle_ui_cannot_remove_any_live_view_and_release_preserves_other_owners() {
    let mut directory = Directory::default();
    directory
        .apply(command("one", &["put"]), Instant::now())
        .unwrap();
    directory
        .apply(command("two", &["call"]), Instant::now())
        .unwrap();
    advance(Duration::from_secs(24 * 60 * 60)).await;
    assert_eq!(directory.demands()[0].required.len(), 2);
    assert!(directory.has_work());
    let mut release = command("missing", &[]);
    release.release = true;
    assert!(!directory.apply(release.clone(), Instant::now()).unwrap());
    release.owner = "one".into();
    assert!(directory.apply(release.clone(), Instant::now()).unwrap());
    assert_eq!(
        directory.demands()[0]
            .required
            .iter()
            .cloned()
            .collect::<Vec<_>>(),
        vec!["call"]
    );
    assert!(!directory.apply(release.clone(), Instant::now()).unwrap());
    release.owner = "two".into();
    directory.apply(release, Instant::now()).unwrap();
    assert!(!directory.has_work());
}
#[tokio::test(start_paused = true)]
async fn full_directory_rejects_new_views_atomically_until_explicit_release() {
    let mut directory = Directory::default();
    for i in 0..64 {
        directory
            .apply(command(&format!("view-{i}"), &["put"]), Instant::now())
            .unwrap();
    }
    advance(Duration::from_secs(3600)).await;
    assert!(
        directory
            .apply(command("overflow", &["call"]), Instant::now())
            .is_err()
    );
    assert_eq!(directory.demands()[0].required.len(), 1);
    let mut conflict = command("view-0", &["new"]);
    conflict.providers = vec!["OTHER".into()];
    assert!(directory.apply(conflict, Instant::now()).is_err());
    let mut release = command("view-0", &[]);
    release.release = true;
    directory.apply(release, Instant::now()).unwrap();
    directory
        .apply(command("replacement", &["call"]), Instant::now())
        .unwrap();
    assert_eq!(directory.demands()[0].required.len(), 2);
    // Restart/ValueDate teardown disposes the entire copied directory.
    assert!(!Directory::default().has_work());
}
#[test]
fn malformed_commands_fail_before_ownership_mutation() {
    let base = json!({"contract":"ES20261218","root":"ES","expiry":739929,"providers":[],"owner":"","required":[],"multiplier":2.5,"wing":"-1","release":false});
    assert!(Command::parse(&base).is_ok());
    for (key, value) in [
        ("contract", json!("")),
        ("expiry", json!(0)),
        ("expiry", json!(3652059)),
        ("providers", json!(["non-ascii-\u{00fc}"])),
        ("owner", json!("a".repeat(1025))),
        ("multiplier", json!(0)),
        ("multiplier", json!(-1)),
        ("wing", json!("not-a-decimal")),
        ("required", json!([""])),
        ("release", json!(null)),
    ] {
        let mut invalid = base.clone();
        invalid[key] = value;
        assert!(Command::parse(&invalid).is_err(), "accepted {key}");
    }
    let mut repeated = base.clone();
    repeated["required"] = json!(["put", "put"]);
    assert_eq!(Command::parse(&repeated).unwrap().required.len(), 1);
}
