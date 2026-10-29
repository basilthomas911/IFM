//! Provider acquisition and atomic catalog publication share one cancellation
//! scope. Dropping any in-flight HTTP future discards its partial replacement;
//! a successful request cannot publish after its original deadline expires.
use super::repository::{Error as RepositoryError, Repository};
use crate::{
    historical::{Failure as HistoryError, History},
    runtime::{Deadline, Interrupted},
};
use std::sync::{Arc, Mutex};
use tokio_util::sync::CancellationToken;

/// A provider-selected universe. Each request is acquired sequentially into one
/// replacement so cross-dataset resolution never sees a half-refreshed catalog.
pub struct Universe {
    /// Exact provider dataset, not inferred from the machine clock.
    pub dataset: String,
    /// Explicit parent selectors; empty requests the entire dataset universe.
    pub parents: Vec<String>,
}
/// Explicit acquisition, cancellation and repository errors.
#[derive(Debug)]
pub enum Failure {
    /// Invalid universe count or duplicate datasets.
    Input,
    /// Original deadline/caller cancellation observed before publication.
    Interrupted(Interrupted),
    /// Provider transport, warnings or decoding failed.
    History(HistoryError),
    /// Bounded catalog mutation failed.
    Repository(RepositoryError),
    /// Repository writer was interrupted by an internal panic.
    Writer,
}
struct Refresh {
    repository: Arc<Mutex<Repository>>,
    token: u64,
    committed: bool,
    cancelled: Arc<std::sync::atomic::AtomicU64>,
}
impl Drop for Refresh {
    fn drop(&mut self) {
        if !self.committed {
            self.cancelled
                .fetch_max(self.token, std::sync::atomic::Ordering::Release);
            if let Ok(mut repository) = self.repository.try_lock() {
                let _ = repository.abort(self.token);
            }
        }
    }
}
/// Acquires at most three dataset universes using the shared, four-request
/// historical client. The guard owns staging cleanup even when this future is
/// dropped by its runtime owner; it never owns or joins a thread/process.
pub async fn refresh(
    history: &History,
    repository: Arc<Mutex<Repository>>,
    universes: &[Universe],
    cancellation: &CancellationToken,
    deadline: Deadline,
) -> Result<u64, Failure> {
    if universes.is_empty()
        || universes.len() > 3
        || universes.iter().enumerate().any(|(i, u)| {
            universes[..i]
                .iter()
                .any(|other| other.dataset == u.dataset)
        })
    {
        return Err(Failure::Input);
    }
    let token = repository
        .lock()
        .map_err(|_| Failure::Writer)?
        .begin()
        .map_err(Failure::Repository)?;
    let cancelled = repository
        .lock()
        .map_err(|_| Failure::Writer)?
        .cancellation_marker();
    let mut guard = Refresh {
        cancelled,
        repository,
        token,
        committed: false,
    };
    for universe in universes {
        history
            .latest_definitions(
                &universe.dataset,
                &universe.parents,
                cancellation,
                deadline,
                |line| {
                    guard
                        .repository
                        .lock()
                        .map_err(|_| "Catalog writer interrupted".to_owned())?
                        .append(token, &universe.dataset, line)
                        .map_err(|error| format!("Catalog hydration failed: {error:?}"))
                },
            )
            .await
            .map_err(Failure::History)?;
    }
    // Cancellation wins even when the final response and timer become ready in
    // the same scheduler turn. No second timeout is started for the commit.
    if cancellation.is_cancelled() {
        return Err(Failure::Interrupted(Interrupted::Cancelled));
    }
    if deadline.remaining().is_zero() {
        return Err(Failure::Interrupted(Interrupted::DeadlineExpired));
    }
    let revision = guard
        .repository
        .lock()
        .map_err(|_| Failure::Writer)?
        .commit(token)
        .map_err(Failure::Repository)?;
    guard.committed = true;
    Ok(revision)
}
#[cfg(test)]
mod cancellation_tests {
    use super::*;
    #[test]
    fn dropped_refresh_never_waits_for_a_catalog_reader_or_commits_cancelled_staging() {
        let repository = Arc::new(Mutex::new(Repository::new(100, 1024 * 1024).unwrap()));
        let token = repository.lock().unwrap().begin().unwrap();
        let cancelled = repository.lock().unwrap().cancellation_marker();
        let refresh = Refresh {
            repository: repository.clone(),
            token,
            committed: false,
            cancelled,
        };
        let mut reader = repository.lock().unwrap();
        drop(refresh);
        assert!(reader.commit(token).is_err());
        assert!(reader.begin().unwrap() > token);
    }
}
