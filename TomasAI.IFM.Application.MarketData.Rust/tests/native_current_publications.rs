//! Capacity, ordering and detached query checks for native latest-value catch-up.
use ifm_market_data_native::{
    dataset::transport::Lane,
    messaging::{current::Current, wire::Frame},
};
fn frame(sequence: i64, size: usize) -> Frame {
    Frame {
        lane: Lane::FuturesGlbx,
        value_date: 739915,
        worker: [1; 16],
        generation: [2; 16],
        manifest_revision: 1,
        sequence,
        kind: 3,
        payload: vec![0xc0; size],
    }
}
#[test]
fn latest_replacement_releases_old_bytes_and_query_copies_survive_retirement() {
    let current = Current::new(2, 16).unwrap();
    current.retain("ES", &frame(1, 8)).unwrap();
    current.retain("VX", &frame(2, 8)).unwrap();
    current.retain("ES", &frame(3, 4)).unwrap();
    assert_eq!(current.bytes().unwrap(), 12);
    let captured = current.capture(0, 2).unwrap();
    assert_eq!(
        captured.iter().map(|f| f.sequence).collect::<Vec<_>>(),
        [2, 3]
    );
    assert_eq!(current.capture(2, 1).unwrap()[0].sequence, 3);
    current.retire().unwrap();
    assert_eq!(current.bytes().unwrap(), 0);
    assert!(current.capture(0, 2).unwrap().is_empty());
    assert_eq!(captured[0].payload.len(), 8);
    assert_eq!(captured[1].payload.len(), 4);
    let mut replacement = frame(1, 4);
    replacement.generation = [3; 16];
    current.retain("ES", &replacement).unwrap();
    assert_eq!(captured[1].generation, [2; 16]);
}
#[test]
fn invalid_stale_and_capacity_failures_are_atomic() {
    let current = Current::new(1, 8).unwrap();
    current.retain("ES", &frame(1, 8)).unwrap();
    assert!(current.retain("VX", &frame(2, 1)).is_err());
    assert!(current.retain("ES", &frame(2, 9)).is_err());
    assert!(current.retain("ES", &frame(1, 4)).is_err());
    let mut stale = frame(2, 4);
    stale.generation = [3; 16];
    assert!(current.retain("ES", &stale).is_err());
    assert_eq!(current.bytes().unwrap(), 8);
    assert_eq!(current.capture(0, 1).unwrap()[0].sequence, 1);
    assert!(current.capture(0, 257).is_err());
    assert!(current.capture(0, 0).is_err());
    assert!(Current::new(0, 8).is_err());
    assert!(Current::new(4097, 8).is_err());
    assert!(Current::new(1, 64 * 1024 * 1024 + 1).is_err());
}
#[test]
fn encoded_query_pages_are_bounded_and_make_progress_without_pinning_the_store() {
    let current = Current::new(8, 8 * 1024 * 1024).unwrap();
    for sequence in 1..=8 {
        current
            .retain(&format!("ES{sequence}"), &frame(sequence, 1024 * 1024))
            .unwrap();
    }
    let mut after = 0;
    let mut all = Vec::new();
    loop {
        let page = current.capture(after, 256).unwrap();
        if page.is_empty() {
            break;
        }
        assert!(page.len() <= 3);
        let encoded: usize = page
            .iter()
            .map(|f| f.content().unwrap().len() + 5)
            .sum::<usize>()
            + 5;
        assert!(encoded <= 4 * 1024 * 1024);
        assert!(page[0].sequence > after);
        after = page.last().unwrap().sequence;
        all.extend(page);
    }
    assert_eq!(
        all.iter().map(|f| f.sequence).collect::<Vec<_>>(),
        (1..=8).collect::<Vec<_>>()
    );
    assert!(current.capture_page(0, 1, 1).is_err());
    assert!(current.capture_page(0, 1, 4 * 1024 * 1024 + 1).is_err());
    current.retire().unwrap();
    assert_eq!(current.bytes().unwrap(), 0);
    assert_eq!(all[0].payload.len(), 1024 * 1024);
}

#[test]
fn complete_capture_rejects_truncation_and_preserves_detached_source_identity() {
    let current = Current::new(2, 16).unwrap();
    current.retain("ES", &frame(1, 8)).unwrap();
    current.retain("VX", &frame(2, 8)).unwrap();
    assert!(current.capture_all(0).is_err());
    assert!(current.capture_all(64 * 1024 * 1024 + 1).is_err());
    assert!(current.capture_all(16 + 512 - 1).is_err());
    let all = current.capture_all(16 + 512).unwrap();
    assert_eq!(all.len(), 2);
    assert_eq!(all[0].sequence, 1);
    assert_eq!(all[1].sequence, 2);
    current.retire().unwrap();
    assert_eq!(all[1].payload.len(), 8);
    assert_eq!(all[1].generation, [2; 16]);
    assert!(current.capture_all(1).unwrap().is_empty());
}

#[test]
fn option_catchup_excludes_durable_evidence_and_checks_only_selected_contract_bytes() {
    let current = Current::new(4, 4096).unwrap();
    let mut evidence = frame(1, 2048);
    evidence.lane = Lane::OptionsGlbx;
    evidence.kind = 6;
    let mut quote = frame(2, 16);
    quote.lane = Lane::OptionsGlbx;
    quote.kind = 7;
    current.retain("ES-option", &evidence).unwrap();
    current.retain("ES-option", &quote).unwrap();
    let retained = current.capture_kind(7, 272).unwrap();
    assert_eq!(retained.len(), 1);
    assert_eq!(retained[0].kind, 7);
    assert_eq!(retained[0].payload, quote.payload);
    assert!(current.capture_kind(7, 271).is_err());
    assert_eq!(current.capture_all(4096).unwrap().len(), 2);
    assert!(current.capture_kind(0, 4096).is_err());
    assert!(current.capture_kind(8, 4096).is_err());
    assert!(current.capture_kind(7, 0).is_err());
    current.retire().unwrap();
    assert!(current.capture_kind(7, 4096).unwrap().is_empty());
    assert_eq!(retained[0].payload.len(), 16);
}
