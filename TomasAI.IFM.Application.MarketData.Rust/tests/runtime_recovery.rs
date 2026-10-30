//! Dataset recovery cannot be certified by partial readiness or old owners.
use ifm_market_data_native::runtime::{
    Deadline,
    owner::Owner,
    recovery::{Phase, Proof, Recovery, Requirements},
};
use std::{sync::Arc, time::Duration};
use tokio::time::advance;
fn all() -> Requirements {
    Requirements::new(&[
        Proof::Transport,
        Proof::Reference,
        Proof::Pricing,
        Proof::Publication,
        Proof::Persistence,
    ])
}
fn ready(r: &Recovery) {
    let ticket = r.status().ticket;
    for proof in [
        Proof::Retired,
        Proof::Transport,
        Proof::Reference,
        Proof::Pricing,
        Proof::Publication,
        Proof::Persistence,
    ] {
        assert!(r.prove(ticket, proof));
    }
    assert_eq!(r.status().phase, Phase::Running);
}
#[tokio::test(start_paused = true)]
async fn transport_proof_alone_does_not_end_reconstruction_but_completed_setup_allows_quiet_quotes() {
    let health = Recovery::new(all(), Deadline::recovery());
    health.monitor_provider();
    let ticket = health.status().ticket;
    assert!(!health.complete_setup(ticket));
    health.prove(ticket, Proof::Retired);
    health.prove(ticket, Proof::Transport);
    advance(Duration::from_secs(119)).await;
    assert_eq!(health.status().deadline.unwrap().remaining(), Duration::from_secs(1));
    assert!(health.complete_setup(ticket));
    advance(Duration::from_secs(900)).await;
    assert_eq!(health.status().phase, Phase::Starting);
    assert!(health.status().deadline.is_none());
    assert!(health.failures(0, 16).is_empty());
    assert!(health.status().missing.contains(&Proof::Pricing));
    // No late old generation may certify a replacement's setup.
    let next = health.replace(all(), Deadline::recovery()).unwrap();
    assert!(!health.complete_setup(ticket));
    assert!(!health.complete_setup(next));
    assert!(health.status().deadline.is_some());
}

#[tokio::test(start_paused = true)]
async fn late_setup_completion_cannot_erase_a_reconstruction_timeout() {
    let health = Recovery::new(all(), Deadline::recovery());
    health.monitor_provider();
    let ticket=health.status().ticket;
    health.prove(ticket, Proof::Retired); health.prove(ticket, Proof::Transport);
    advance(Duration::from_secs(120)).await;
    assert!(!health.complete_setup(ticket));
    assert_eq!(health.status().phase, Phase::Failed);
    assert_eq!(health.failures(0,16).len(),1);
}
#[tokio::test(start_paused = true)]
async fn retries_and_replacements_cannot_extend_original_two_minute_window() {
    let root = Arc::new(Owner::default());
    let r = Recovery::new(all(), Deadline::recovery());
    let watchdog = r.watchdog(&root).unwrap();
    let initial = r.status().ticket;
    advance(Duration::from_secs(40)).await;
    r.fault(initial, "original provider error", Deadline::recovery())
        .unwrap();
    assert_eq!(
        r.status().deadline.unwrap().remaining(),
        Duration::from_secs(80)
    );
    advance(Duration::from_secs(40)).await;
    let replacement = r.replace(all(), Deadline::recovery()).unwrap();
    assert!(!r.prove(initial, Proof::Transport));
    assert_eq!(
        r.status().deadline.unwrap().remaining(),
        Duration::from_secs(40)
    );
    for proof in [
        Proof::Retired,
        Proof::Transport,
        Proof::Reference,
        Proof::Pricing,
        Proof::Persistence,
    ] {
        r.prove(replacement, proof);
    }
    assert_eq!(r.status().missing, vec![Proof::Publication]);
    let mut notifications = r.subscribe();
    advance(Duration::from_secs(40)).await;
    // The watchdog, not quote traffic or status polling, must publish expiry.
    tokio::task::yield_now().await;
    notifications.changed().await.unwrap();
    assert_eq!(notifications.borrow().phase, Phase::Failed);
    let failed = r.status();
    assert_eq!(
        failed.root_error.as_deref(),
        Some("original provider error")
    );
    assert!(!r.prove(replacement, Proof::Publication));
    assert!(
        r.fault(replacement, "later noise", Deadline::recovery())
            .is_none()
    );
    advance(Duration::from_secs(600)).await;
    assert_eq!(r.status().phase, Phase::Failed);
    root.stop().unwrap();
    watchdog.await.unwrap();
}
#[tokio::test(start_paused = true)]
async fn complete_recovery_resets_allowance_but_partial_recovery_does_not() {
    let r = Recovery::new(all(), Deadline::recovery());
    let first = r.status().ticket;
    advance(Duration::from_secs(119)).await;
    ready(&r);
    advance(Duration::from_secs(500)).await;
    let second = r
        .fault(first, "new incident", Deadline::recovery())
        .unwrap();
    assert_ne!(second, first);
    assert!(!r.prove(first, Proof::Publication));
    assert_eq!(
        r.status().deadline.unwrap().remaining(),
        Duration::from_secs(120)
    );
    advance(Duration::from_secs(110)).await;
    r.fault(second, "pricing retry failed", Deadline::recovery())
        .unwrap();
    assert_eq!(
        r.status().deadline.unwrap().remaining(),
        Duration::from_secs(10)
    );
    ready(&r);
    let third = r
        .fault(second, "independent error", Deadline::recovery())
        .unwrap();
    assert_ne!(second, third);
    assert_eq!(r.status().root_error.as_deref(), Some("independent error"));
}
#[tokio::test(start_paused = true)]
async fn deadline_boundary_idle_and_explicit_retry_are_observable_without_admission_quarantine() {
    let idle = Requirements::new(&[]);
    let r = Recovery::new(idle, Deadline::after(Duration::ZERO));
    assert!(!r.prove(r.status().ticket, Proof::Retired));
    assert_eq!(r.status().phase, Phase::Failed);
    let retry = r.replace(idle, Deadline::recovery()).unwrap();
    assert!(r.prove(retry, Proof::Retired));
    assert_eq!(r.status().phase, Phase::Running);
    let active = r.replace(all(), Deadline::recovery()).unwrap();
    assert_eq!(r.status().missing.len(), 6);
    assert!(!r.prove(retry, Proof::Publication));
    advance(Duration::from_secs(120)).await;
    assert!(!r.prove(active, Proof::Retired));
    assert_eq!(r.status().phase, Phase::Failed);
    r.stop();
    assert!(r.replace(idle, Deadline::recovery()).is_none());
    assert_eq!(r.status().phase, Phase::Stopped);
}
#[tokio::test(start_paused = true)]
async fn earlier_fault_budget_bounded_utf8_and_dataset_isolation() {
    let r = Recovery::new(all(), Deadline::recovery());
    let sibling = Recovery::new(all(), Deadline::recovery());
    ready(&sibling);
    r.fault(
        r.status().ticket,
        &"\u{1f680}".repeat(5000),
        Deadline::after(Duration::from_secs(5)),
    )
    .unwrap();
    assert_eq!(r.status().root_error.unwrap().len(), 4096);
    advance(Duration::from_secs(5)).await;
    assert_eq!(r.status().phase, Phase::Failed);
    assert_eq!(sibling.status().phase, Phase::Running);
    assert!(sibling.status().deadline.is_none());
}

#[tokio::test(start_paused = true)]
async fn terminal_messages_survive_retry_and_stop_and_remain_bounded_until_ui_acknowledgment() {
    let r = Recovery::new(all(), Deadline::recovery());
    for episode in 0..20 {
        r.fault(
            r.status().ticket,
            &format!("root cause {episode}"),
            Deadline::recovery(),
        )
        .unwrap();
        advance(Duration::from_secs(120)).await;
        assert_eq!(r.status().phase, Phase::Failed);
        let records = r.failures(0, usize::MAX);
        assert_eq!(records.len(), (episode + 1).min(16));
        assert_eq!(
            records.last().unwrap().status.root_error.as_deref(),
            Some(format!("root cause {episode}").as_str())
        );
        assert_eq!(
            r.failures(0, usize::MAX).len(),
            records.len(),
            "status queries cannot consume failures"
        );
        r.replace(all(), Deadline::recovery()).unwrap();
    }
    let records = r.failures(0, 16);
    assert_eq!(records.iter().map(|record| record.replaced).sum::<u64>(), 4);
    assert!(
        !r.acknowledge_failures(999),
        "future acknowledgment cannot erase pending UI messages"
    );
    r.stop();
    assert_eq!(
        r.failures(0, 16).len(),
        16,
        "resource stop does not erase terminal UI evidence"
    );
    let middle = records[7].ordinal;
    assert!(r.acknowledge_failures(middle));
    assert_eq!(r.failures(0, 16).len(), 8);
    assert!(
        r.failures(0, 16)
            .iter()
            .all(|record| record.ordinal > middle)
    );
    let last = records.last().unwrap().ordinal;
    assert!(r.acknowledge_failures(last));
    assert!(r.failures(0, 16).is_empty());
}

#[tokio::test(start_paused = true)]
async fn passive_expiry_never_reopens_terminal_recovery_at_or_after_deadline() {
    for delay in [120, 121, 720] {
        let r = Recovery::new(all(), Deadline::recovery());
        let ticket = r.status().ticket;
        advance(Duration::from_secs(119)).await;
        r.replace_passive(all(), Deadline::recovery()).unwrap();
        assert_eq!(
            r.status().deadline.unwrap().remaining(),
            Duration::from_secs(1)
        );
        advance(Duration::from_secs(delay - 119)).await;
        assert!(
            r.replace_passive(Requirements::new(&[]), Deadline::recovery())
                .is_none()
        );
        let failed = r.status();
        assert_eq!(failed.phase, Phase::Failed);
        assert_ne!(failed.ticket, ticket);
        assert_eq!(r.failures(0, 16).len(), 1);
        for _ in 0..10 {
            assert!(r.replace_passive(all(), Deadline::recovery()).is_none());
            assert_eq!(r.status().ticket, failed.ticket);
        }
        assert_eq!(r.failures(0, 16).len(), 1);
        assert!(r.replace(all(), Deadline::recovery()).is_some());
        assert_eq!(
            r.status().deadline.unwrap().remaining(),
            Duration::from_secs(120)
        );
        assert_eq!(r.failures(0, 16).len(), 1);
    }
}

#[tokio::test(start_paused = true)]
async fn first_provider_silence_starts_full_recovery_at_detection_not_app_start() {
    let health = Recovery::new(Requirements::new(&[Proof::Transport]), Deadline::recovery());
    health.replace_passive(Requirements::new(&[Proof::Transport]), Deadline::recovery());
    assert_eq!(health.status().phase, Phase::Starting);
    let ticket = health.status().ticket;
    advance(Duration::from_secs(40)).await;
    let incident = health
        .provider_fault(ticket, "heartbeat silence exceeded 40 seconds")
        .unwrap();
    assert_eq!(health.status().phase, Phase::Recovering);
    advance(Duration::from_secs(119)).await;
    assert_eq!(health.status().phase, Phase::Recovering);
    health.provider_fault(incident, "repeated connection failure");
    advance(Duration::from_secs(1)).await;
    assert_eq!(health.status().phase, Phase::Failed);
    assert_eq!(health.failures(0, 16).len(), 1);
}
