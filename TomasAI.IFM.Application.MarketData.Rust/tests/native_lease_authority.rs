//! Native-only lifetime, race-order and bounded ownership regression tests.
use ifm_market_data_native::subscriptions::{
    Failure,
    leases::{Leases, Lifetime},
};
use std::time::Duration;
use tokio::time::Instant;

fn authority() -> Leases {
    Leases::new(1, 8, 16, 32, Duration::from_secs(120)).unwrap()
}
fn ttl(seconds: u64) -> Lifetime {
    Lifetime::Ephemeral(Duration::from_secs(seconds))
}

#[test]
fn native_expiry_retires_only_temporary_dependencies_and_preserves_durable_intent() {
    let now = Instant::now();
    let mut leases = authority();
    let a = leases
        .acquire(1, 10, &[1, 2], ttl(30), now)
        .unwrap()
        .token
        .unwrap();
    leases
        .acquire(1, 20, &[2, 3], Lifetime::Durable, now)
        .unwrap();
    assert_eq!(
        leases.next_expiration(),
        Some(now + Duration::from_secs(30))
    );
    assert!(leases.expire(now + Duration::from_secs(29)).is_empty());
    let deltas = leases.expire(now + Duration::from_secs(30));
    assert_eq!(deltas.len(), 1);
    assert_eq!(deltas[0].unsubscribe, vec![1]);
    assert_eq!(leases.instruments(), vec![2, 3]);
    assert_eq!(leases.next_expiration(), None);
    assert_eq!(
        leases.renew(a, Duration::from_secs(60), now + Duration::from_secs(30)),
        Err(Failure::StaleGeneration)
    );
}

#[test]
fn renewal_uses_monotonic_native_time_without_a_provider_gate_and_never_revives_expiry() {
    let now = Instant::now();
    let mut leases = authority();
    let token = leases
        .acquire(1, 10, &[1], ttl(30), now)
        .unwrap()
        .token
        .unwrap();
    leases
        .renew(
            token,
            Duration::from_secs(60),
            now + Duration::from_secs(20),
        )
        .unwrap();
    assert!(leases.expire(now + Duration::from_secs(30)).is_empty());
    let expired = now + Duration::from_secs(80);
    assert_eq!(
        leases.renew(token, Duration::from_secs(60), expired),
        Err(Failure::StaleGeneration)
    );
    assert_eq!(
        leases.acquire(1, 10, &[1], ttl(30), expired),
        Err(Failure::StaleGeneration)
    );
    leases.expire(expired);
    let fresh = leases
        .acquire(1, 10, &[1], ttl(30), expired)
        .unwrap()
        .token
        .unwrap();
    assert_ne!(token, fresh);
    assert!(leases.release(token).unsubscribe.is_empty());
    assert_eq!(leases.instruments(), vec![1]);
}

#[test]
fn generation_replacement_preserves_original_expiration_without_renewing_it() {
    let now = Instant::now();
    let mut leases = authority();
    let old = leases
        .acquire(1, 10, &[1, 2], ttl(30), now)
        .unwrap()
        .token
        .unwrap();
    leases
        .acquire(1, 20, &[2, 3], Lifetime::Durable, now)
        .unwrap();
    let original = leases.next_expiration();
    let tokens = leases.replace_generation(2).unwrap();
    assert_eq!(leases.next_expiration(), original);
    assert_eq!(tokens.len(), 2);
    assert!(leases.release(old).unsubscribe.is_empty());
    assert_eq!(
        leases.renew(old, Duration::from_secs(60), now),
        Err(Failure::StaleGeneration)
    );
    assert_eq!(
        leases.expire(now + Duration::from_secs(30))[0].unsubscribe,
        vec![1]
    );
    assert_eq!(leases.instruments(), vec![2, 3]);
}

#[test]
fn invalid_lifetimes_and_capacity_do_not_partially_change_live_owners() {
    let now = Instant::now();
    let mut leases = Leases::new(1, 1, 2, 2, Duration::from_secs(120)).unwrap();
    let first = leases
        .acquire(1, 10, &[1, 2], Lifetime::Durable, now)
        .unwrap()
        .token
        .unwrap();
    assert_eq!(
        leases.acquire(1, 20, &[3], ttl(30), now),
        Err(Failure::Capacity)
    );
    assert_eq!(
        leases.acquire(1, 10, &[3], ttl(0), now),
        Err(Failure::InvalidInput)
    );
    assert_eq!(
        leases.acquire(1, 10, &[3], ttl(121), now),
        Err(Failure::InvalidInput)
    );
    assert_eq!(
        leases.renew(first, Duration::from_secs(60), now),
        Err(Failure::StaleGeneration)
    );
    assert_eq!(leases.token(10), Some(first));
    assert_eq!(leases.instruments(), vec![1, 2]);
}

#[test]
fn thousand_replacements_and_stale_releases_keep_bounded_native_ownership() {
    let now = Instant::now();
    let mut leases = authority();
    let first = leases
        .acquire(1, 10, &[1, 2], Lifetime::Durable, now)
        .unwrap()
        .token
        .unwrap();
    let mut previous = first;
    for generation in 2..=1001 {
        let tokens = leases.replace_generation(generation).unwrap();
        assert_eq!(tokens.len(), 1);
        assert!(leases.release(previous).unsubscribe.is_empty());
        assert_eq!(leases.instruments(), vec![1, 2]);
        previous = tokens[0];
    }
    assert_eq!(leases.release(previous).unsubscribe, vec![1, 2]);
    assert!(leases.instruments().is_empty());
}
