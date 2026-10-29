//! Separate caller import duration from the nonextendable dataset recovery cap.
use ifm_market_data_native::runtime::{Deadline, Interrupted};
use std::time::Duration;
use tokio_util::sync::CancellationToken;
#[tokio::test]
async fn imports_keep_original_long_budget_and_expired_work_is_never_polled() {
    let import = Deadline::operation(Duration::from_secs(25 * 60));
    assert!(import.remaining() > Duration::from_secs(24 * 60));
    let bounded = import.earliest(Deadline::recovery());
    assert!(bounded.remaining() <= Duration::from_secs(120));
    assert!(Deadline::operation(Duration::MAX).remaining() <= Duration::from_secs(30 * 60));
    assert_eq!(
        Deadline::operation(Duration::ZERO)
            .run(&CancellationToken::new(), async {
                panic!("expired historical work was polled")
            })
            .await,
        Err(Interrupted::DeadlineExpired)
    );
}
