//! Fresh runtime leases and reference counts, independent of durable renewal.
use ifm_market_data_native::subscriptions::{Failure, Owners};
#[test]
fn overlapping_dependencies_last_release_and_generation_reacquisition() {
    let mut owners = Owners::new(1, 8, 8, 16).unwrap();
    let first = owners.acquire(1, 100, &[42, 43]).unwrap();
    let a = first.token.unwrap();
    assert_eq!(first.subscribe, vec![42, 43]);
    let b = owners.acquire(1, 200, &[43, 44]).unwrap().token.unwrap();
    assert_eq!(owners.references(43), 2);
    assert_eq!(owners.acquire(1, 100, &[43, 42]).unwrap().token, Some(a));
    assert_eq!(owners.release(a).unsubscribe, vec![42]);
    assert_eq!(owners.instruments(), vec![43, 44]);
    assert!(owners.release(a).unsubscribe.is_empty());
    let a2 = owners.acquire(1, 100, &[43, 44]).unwrap().token.unwrap();
    assert_ne!(a, a2);
    assert!(owners.release(a).unsubscribe.is_empty());
    let restored = owners.replace_generation(2).unwrap();
    assert_eq!(restored.len(), 2);
    assert!(
        restored
            .iter()
            .all(|t| t.generation == 2 && t.serial > a2.serial)
    );
    assert!(owners.release(b).unsubscribe.is_empty());
    assert_eq!(owners.instruments(), vec![43, 44]);
    let a3 = owners.token(100).unwrap();
    let b3 = owners.token(200).unwrap();
    assert!(owners.release(a3).unsubscribe.is_empty());
    assert_eq!(owners.release(b3).unsubscribe, vec![43, 44]);
}
#[test]
fn capacity_failures_are_atomic_and_stale_release_cannot_remove_changed_owner() {
    let mut owners = Owners::new(1, 2, 3, 4).unwrap();
    let a = owners.acquire(1, 1, &[1, 2]).unwrap().token.unwrap();
    let b = owners.acquire(1, 2, &[2, 3]).unwrap().token.unwrap();
    assert_eq!(owners.acquire(1, 3, &[1]), Err(Failure::Capacity));
    assert_eq!(owners.acquire(1, 1, &[1, 2, 3]), Err(Failure::Capacity));
    assert_eq!(owners.acquire(1, 1, &[1, 4]), Err(Failure::Capacity));
    assert_eq!(owners.acquire(2, 1, &[1]), Err(Failure::StaleGeneration));
    assert_eq!(owners.acquire(1, 1, &[1, 1]), Err(Failure::InvalidInput));
    assert_eq!(owners.token(1), Some(a));
    assert_eq!(owners.token(2), Some(b));
    assert_eq!(owners.instruments(), vec![1, 2, 3]);
    let changed = owners.acquire(1, 1, &[3]).unwrap().token.unwrap();
    assert_ne!(changed, a);
    assert!(owners.release(a).unsubscribe.is_empty());
    assert_eq!(owners.references(3), 2);
    for generation in 2..=1001 {
        let previous = owners.token(1).unwrap();
        owners.replace_generation(generation).unwrap();
        assert!(owners.release(previous).unsubscribe.is_empty());
        assert_eq!(owners.instruments(), vec![2, 3]);
    }
}
