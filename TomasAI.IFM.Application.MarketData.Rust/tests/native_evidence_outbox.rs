//! Persistence acknowledgments are tested separately from transport receipts.
use ifm_market_data_native::{
    messaging::{
        evidence_outbox::Outbox,
        option_evidence::{Evidence, Failure, Outcome},
        option_source::Source,
    },
    reference::decimal,
};
use std::sync::Arc;

fn evidence(sequence: i64) -> Evidence {
    Evidence {
        source: Source {
            dataset: "GLBX.MDP3".into(),
            publisher: 3,
            instrument: 42,
            contract: "ES-option".into(),
            date: time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap(),
            price: decimal::parse("10.25").unwrap(),
            size: 1,
            sequence,
            event: 1_793_102_400_000_000_000,
            received: 1_793_102_400_000_000_100,
            generation: [1; 16],
            symbol: "fixture".into(),
        },
        context: None,
        underlying: None,
        at: 17_931_024_000_000_001,
        outcome: Outcome::Failure(Failure {
            code: "PricingContextUnavailable".into(),
            input: "OptionPricing".into(),
            contract: "ES-option".into(),
            detail: "Qualified calculation is unavailable.".into(),
            retryable: false,
        }),
    }
}
#[test]
fn first_attempt_and_durable_ack_are_independent_of_transport_and_replay() {
    let box_ = Outbox::new(2, 1024 * 1024).unwrap();
    let first = evidence(1);
    let retained = box_.retain(&first).unwrap();
    let mut replay = first.clone();
    replay.source.received += 100;
    replay.source.generation = [2; 16];
    replay.at += 200;
    assert_ne!(first.encode().unwrap(), replay.encode().unwrap());
    let duplicate = box_.retain(&replay).unwrap();
    assert!(Arc::ptr_eq(&retained, &duplicate));
    assert_eq!(retained.generation, first.source.generation);
    assert_eq!(&*retained.bytes, first.encode().unwrap());
    assert!(box_.capture(true, 64).unwrap().is_empty());
    assert!(box_.complete(&retained.identity, retained.receipt).is_err());
    assert!(
        box_.acknowledge(&retained.identity, "wrong", retained.receipt)
            .is_err()
    );
    assert!(
        box_.acknowledge(&retained.identity, &retained.digest, retained.receipt + 1)
            .is_err()
    );
    replay.source.price = decimal::parse("11").unwrap();
    assert!(box_.retain(&replay).is_err());
    assert_eq!(box_.retained_bytes().unwrap(), retained.bytes.len());
    box_.acknowledge(&retained.identity, &retained.digest, retained.receipt)
        .unwrap();
    box_.acknowledge(&retained.identity, &retained.digest, retained.receipt)
        .unwrap();
    assert!(box_.capture(false, 64).unwrap().is_empty());
    assert_eq!(box_.capture(true, 64).unwrap()[0].receipt, retained.receipt);
    box_.complete(&retained.identity, retained.receipt).unwrap();
    assert_eq!(box_.retained_bytes().unwrap(), 0);
    let replacement = box_.retain(&first).unwrap();
    assert_ne!(replacement.receipt, retained.receipt);
    assert!(
        box_.acknowledge(&retained.identity, &retained.digest, retained.receipt)
            .is_err()
    );
    assert!(box_.complete(&retained.identity, retained.receipt).is_err());
    assert_eq!(retained.bytes.as_ref(), first.encode().unwrap());
}
#[test]
fn bounded_storage_and_detached_consumers_do_not_block_native_owner_disposal() {
    use ifm_market_data_native::runtime::owner::Owner;
    let first = evidence(1);
    let size = first.encode().unwrap().len();
    let box_ = Arc::new(Outbox::new(2, size * 2).unwrap());
    let mut notices = box_.changes();
    let one = box_.retain(&first).unwrap();
    assert!(notices.has_changed().unwrap());
    notices.borrow_and_update();
    let two = box_.retain(&evidence(2)).unwrap();
    assert!(box_.retain(&evidence(3)).is_err());
    assert_eq!(box_.retained_bytes().unwrap(), size * 2);
    assert!(box_.capture(false, 0).is_err());
    assert!(box_.capture(false, 65).is_err());
    let order = box_.capture(false, 64).unwrap();
    assert!(order[0].receipt < order[1].receipt);
    let owner = Arc::new(Owner::default());
    let child = owner.child().unwrap();
    drop(child);
    drop(owner); // No evidence record contains an Owner/Child or provider reference.
    box_.acknowledge(&one.identity, &one.digest, one.receipt)
        .unwrap();
    box_.complete(&one.identity, one.receipt).unwrap();
    box_.acknowledge(&two.identity, &two.digest, two.receipt)
        .unwrap();
    box_.complete(&two.identity, two.receipt).unwrap();
    assert_eq!(box_.retained_bytes().unwrap(), 0);
    drop(box_);
    assert_eq!(order[0].bytes.len(), size);
    assert!(Outbox::new(0, 1).is_err());
    assert!(Outbox::new(1, 0).is_err());
    assert!(Outbox::new(4097, 1).is_err());
    assert!(Outbox::new(1, 64 * 1024 * 1024 + 1).is_err());
    assert!(Outbox::new(1, size - 1).unwrap().retain(&first).is_err());
}

#[test]
fn retired_evidence_is_bounded_accounted_and_never_relabels_source_records() {
    use ifm_market_data_native::messaging::retired_evidence::{Accounting, Archive};
    let mut archive = Archive::default();
    let empty = Arc::new(Outbox::new(2, 1024 * 1024).unwrap());
    archive.retain(empty).unwrap();
    assert_eq!(archive.status(), (0, Accounting::default()));
    let mut first_size = 0;
    let mut latest = None;
    for generation in 1..=8 {
        let outbox = Arc::new(Outbox::new(2, 1024 * 1024).unwrap());
        let mut value = evidence(generation);
        value.source.generation = [generation as u8; 16];
        let record = outbox.retain(&value).unwrap();
        if generation == 1 {
            first_size = record.bytes.len();
        }
        archive.retain(outbox.clone()).unwrap();
        archive.retain(outbox.clone()).unwrap();
        let (count, accounting) = archive.status();
        assert_eq!(count, (generation as usize).min(4));
        assert_eq!(
            accounting.generations,
            generation.saturating_sub(4).max(0) as u64
        );
        assert_eq!(accounting.records, accounting.generations);
        assert_eq!(accounting.bytes, accounting.generations * first_size as u64);
        assert!(Arc::ptr_eq(&archive.latest().unwrap(), &outbox));
        latest = Some(outbox);
    }
    let latest = latest.unwrap();
    let record = latest.capture(false, 1).unwrap().pop().unwrap();
    assert_eq!(record.generation, [8; 16]);
    latest
        .acknowledge(&record.identity, &record.digest, record.receipt)
        .unwrap();
    latest.complete(&record.identity, record.receipt).unwrap();
    let next = Arc::new(Outbox::new(1, 1024 * 1024).unwrap());
    next.retain(&evidence(9)).unwrap();
    archive.retain(next).unwrap();
    assert_eq!(archive.status().0, 4);
    assert_eq!(archive.status().1.generations, 4);
}
