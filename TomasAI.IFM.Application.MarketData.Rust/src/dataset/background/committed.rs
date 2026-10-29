//! Live committed handoff processing beneath the existing dataset parent. New
//! business watermarks are observed without replacing unchanged physical sources.
use super::*;
impl Configuration {
    pub(super) async fn refresh_committed_in_place(
        &self,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
        physical: Option<[u8; 32]>,
        ready: bool,
    ) -> Result<bool, String> {
        let Some(directory) = &self.committed else {
            return Ok(true);
        };
        loop {
            if stop.is_cancelled() || deadline.remaining().is_zero() {
                return Err("Committed handoff cancelled/original deadline expired".into());
            }
            let loaded = directory
                .load(
                    || {
                        self.discovery
                            .read_committed("IFM", parent.clone(), stop, deadline)
                    },
                    stop,
                    deadline,
                )
                .await;
            let book = match loaded {
                Ok(book) => book,
                Err(ref error)
                    if superseded(error)
                        && !directory.current()?
                        && !stop.is_cancelled()
                        && !deadline.remaining().is_zero() =>
                {
                    continue;
                }
                Err(error) => return Err(error),
            };
            if let Some(physical) = physical
                && book.physical_signature()? != physical
            {
                return Ok(false);
            }
            if ready {
                match directory
                    .deliver_for_physical(&self.discovery, parent.clone(), stop, deadline, physical)
                    .await
                {
                    Ok(accepted) => return Ok(accepted),
                    Err(ref error)
                        if superseded(error)
                            && !directory.current()?
                            && !stop.is_cancelled()
                            && !deadline.remaining().is_zero() =>
                    {
                        continue;
                    }
                    Err(error) => return Err(error),
                }
            }
            return Ok(true);
        }
    }
}

// Only internal compare-and-swap races are retried in place. Unknown storage,
// parsing and persistence errors immediately enter supervised dataset recovery.
pub(super) fn superseded(error: &str) -> bool {
    matches!(
        error,
        "Committed source was superseded by a newer handoff"
            | "Committed capture retired before acceptance"
            | "Committed journal changed before delivery acknowledgement"
            | "Committed journal changed during delivery acknowledgement"
            | "Committed acknowledgement was superseded"
    )
}
