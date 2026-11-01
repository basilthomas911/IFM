//! Bounded execution for large, side-effect-free decode jobs. A cancelled
//! waiter discards its result; its running job retains the concurrency permit.
use std::sync::{Arc, OnceLock};

/// Limits simultaneous CPU-heavy decode jobs independently of async request workers.
pub struct ComputePool {
    /// Slots associated with this compute pool.
    slots: Arc<tokio::sync::Semaphore>,
}
impl ComputePool {
    /// Constructs ComputePool from the supplied inputs.
    pub fn new(capacity: usize) -> Self {
        Self {
            slots: Arc::new(tokio::sync::Semaphore::new(capacity)),
        }
    }


    /// Provides run for the compute pool implementation.
    pub async fn run<T: Send + 'static, F: FnOnce() -> Result<T, String> + Send + 'static>(
        &self,
        deadline: tokio::time::Instant,
        job: F,
    ) -> Result<T, String> {
        tokio::time::timeout_at(deadline, async {
            let permit = self
                .slots
                .clone()
                .acquire_owned()
                .await
                .map_err(|_| "Compute executor closed".to_string())?;
            tokio::task::spawn_blocking(move || {
                let _permit = permit;
                job()
            })
            .await
            .map_err(|e| format!("Decode worker failed: {e}"))?
        })
        .await
        .map_err(|_| "Backend decode deadline expired".to_string())?
    }
}


/// Decodes compute for compute.
pub async fn decode<T: crate::models::direct_backend::DirectRecord + Send + 'static>(
    plain: std::borrow::Cow<'_, [u8]>,
    deadline: tokio::time::Instant,
) -> Result<T, String> {
    // Small responses stay inline; large responses cannot monopolize either of
    // the two native async service workers. Wire/message bounds remain unchanged.
    if plain.len() < 256 * 1024 {
        return crate::models::direct_backend::decode_plain_service(plain.as_ref());
    }
    static POOL: OnceLock<ComputePool> = OnceLock::new();
    let bytes = plain.into_owned();
    POOL.get_or_init(|| ComputePool::new(2))
        .run(deadline, move || {
            crate::models::direct_backend::decode_plain_service(&bytes)
        })
        .await
}
#[cfg(test)]
mod tests {
    use super::*;
    use std::time::Duration;


    /// Verifies that cancelling a waiter does not release a running jobs permit.
    #[tokio::test(flavor = "multi_thread", worker_threads = 2)]
    async fn cancelling_a_waiter_does_not_release_a_running_jobs_permit() {
        let pool = Arc::new(ComputePool::new(1));
        let (started, rx) = std::sync::mpsc::channel();
        let (release, wait) = std::sync::mpsc::channel();
        let worker_pool = pool.clone();
        let job = tokio::spawn(async move {
            worker_pool
                .run(
                    tokio::time::Instant::now() + Duration::from_secs(5),
                    move || {
                        started.send(()).unwrap();
                        wait.recv().unwrap();
                        Ok(42)
                    },
                )
                .await
        });
        tokio::task::spawn_blocking(move || rx.recv_timeout(Duration::from_secs(2)).unwrap())
            .await
            .unwrap();
        job.abort();
        let _ = job.await;
        assert_eq!(pool.slots.available_permits(), 0);
        let result = pool.run(tokio::time::Instant::now(), || Ok(7)).await;
        assert!(result.is_err());
        release.send(()).unwrap();
        assert_eq!(
            pool.run(tokio::time::Instant::now() + Duration::from_secs(2), || Ok(
                8
            ))
            .await
            .unwrap(),
            8
        );
        let failure: Result<(), String> = pool
            .run(tokio::time::Instant::now() + Duration::from_secs(2), || {
                Err("fixture decode failure".into())
            })
            .await;
        assert_eq!(failure.unwrap_err(), "fixture decode failure");
    }
}
