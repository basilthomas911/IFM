//! Operational timings only: never use this clock for ValueDate or trading decisions.
//! One correlated JSON record per load, including failed/cancelled loads. Nested
//! durations overlap (notably parallel positions/limits); do not sum them.
use serde_json::{json, Value};
use std::{
    future::Future,
    sync::{Arc, Mutex},
    time::Instant,
};

tokio::task_local! { static CURRENT: Arc<Trace>; }

/// Trace used by the trade load component.
struct Trace {
    /// Id associated with this trace.
    id: String,
    /// Started associated with this trace.
    started: Instant,
    /// Stages associated with this trace.
    stages: Mutex<Vec<Value>>,
}

/// Stage used by the trade load component.
struct Stage {
    /// Trace associated with this stage.
    trace: Arc<Trace>,
    /// Name associated with this stage.
    name: &'static str,
    /// Started associated with this stage.
    started: Instant,
    /// Outcome associated with this stage.
    outcome: &'static str,
}
impl Stage {
    /// Constructs Stage from the supplied inputs.
    fn new(name: &'static str) -> Option<Self> {
        CURRENT
            .try_with(|trace| Self {
                trace: trace.clone(),
                name,
                started: Instant::now(),
                outcome: "interrupted",
            })
            .ok()
    }
}
impl Drop for Stage {
    /// Releases resources owned by this value when its lifetime ends.
    fn drop(&mut self) {
        if let Ok(mut stages) = self.trace.stages.lock() {
            stages.push(json!({"stage": self.name, "start_ms": self.started.duration_since(self.trace.started).as_secs_f64()*1000.0,
                "duration_ms": self.started.elapsed().as_secs_f64()*1000.0, "outcome": self.outcome}));
        }
    }
}


/// Measure awaited backend work, including deserialization, without changing its result.
pub async fn measure<T, E>(
    name: &'static str,
    work: impl Future<Output = Result<T, E>>,
) -> Result<T, E> {
    let mut stage = Stage::new(name);
    let result = work.await;
    if let Some(stage) = &mut stage {
        stage.outcome = if result.is_ok() { "ok" } else { "error" };
    }
    result
}


/// Records the elapsed time and outcome of the supplied synchronous operation.
pub fn measure_sync<T, E>(name: &'static str, work: impl FnOnce() -> Result<T, E>) -> Result<T, E> {
    let mut stage = Stage::new(name);
    let result = work();
    if let Some(stage) = &mut stage {
        stage.outcome = if result.is_ok() { "ok" } else { "error" };
    }
    result
}


/// Provides mark for the trade load implementation.
pub fn mark(name: &'static str) {
    if let Some(mut stage) = Stage::new(name) {
        stage.outcome = "ok";
    }
}

/// Completion used by the trade load component.
struct Completion {
    /// Event associated with this completion.
    event: &'static str,
    /// Sequence associated with this completion.
    sequence: u64,
    /// Trace associated with this completion.
    trace: Arc<Trace>,
    /// Outcome associated with this completion.
    outcome: &'static str,
    /// Selection associated with this completion.
    selection: Value,
}
impl Completion {
    /// Records trade load for completion.
    fn record(&self) -> Value {
        json!({"event":self.event, "load_id":self.trace.id,"process_id":std::process::id(),"process_load_number":self.sequence,
            "observed_at_utc":chrono::Utc::now().to_rfc3339(),"source":if std::env::var_os("IFM_STANDALONE_TEST_NATS_URL").is_some(){"isolated-test"}else{"ui"},"selection":self.selection,
            "outcome":self.outcome,"total_ms":self.trace.started.elapsed().as_secs_f64()*1000.0,
            "stages":self.trace.stages.lock().map(|s|s.clone()).unwrap_or_default()})
    }
}
impl Drop for Completion {
    /// Releases resources owned by this value when its lifetime ends.
    fn drop(&mut self) {
        write_record(&self.record());
    }
}


/// Scope survives await points but not spawned tasks; all measured load stages
/// are awaited here. Dropping the deadline future records an interrupted load.
pub async fn run<T, E>(selection: Value, work: impl Future<Output = Result<T, E>>) -> Result<T, E> {
    let trace = Arc::new(Trace {
        id: uuid::Uuid::new_v4().to_string(),
        started: Instant::now(),
        stages: Mutex::new(Vec::new()),
    });
    static LOAD_SEQUENCE: std::sync::atomic::AtomicU64 = std::sync::atomic::AtomicU64::new(0);
    let sequence = LOAD_SEQUENCE.fetch_add(1, std::sync::atomic::Ordering::Relaxed) + 1;
    let mut completion = Completion {
        event: "trade_load_timing",
        sequence,
        trace: trace.clone(),
        outcome: "interrupted",
        selection,
    };
    let result = CURRENT.scope(trace, work).await;
    completion.outcome = if result.is_ok() { "ok" } else { "error" };
    result
}


/// Returns the identifier associated with the current diagnostic operation.
pub fn current_id() -> Option<String> {
    CURRENT.try_with(|trace| trace.id.clone()).ok()
}


/// Optional hydration is a separate duration; it is not part of foreground load latency.
pub async fn background<T, E>(
    selection: Value,
    work: impl Future<Output = Result<T, E>>,
) -> Result<T, E> {
    let trace = Arc::new(Trace {
        id: uuid::Uuid::new_v4().to_string(),
        started: Instant::now(),
        stages: Mutex::new(Vec::new()),
    });
    let mut completion = Completion {
        event: "trade_load_background_timing",
        sequence: 0,
        trace: trace.clone(),
        outcome: "interrupted",
        selection,
    };
    let result = CURRENT.scope(trace, work).await;
    completion.outcome = if result.is_ok() { "ok" } else { "error" };
    result
}


/// Writes record for trade load.
fn write_record(record: &Value) {
    // Serialize once per user load, not on market ticks. Failure to write a
    // diagnostic must never prevent opening a trade or alter recovery behavior.
    let line = record.to_string();
    eprintln!("{line}");
    super::writer::timing(line);
}

#[cfg(test)]
mod tests {
    use super::*;


    /// Verifies that stages preserve results and record failures and cancellation.
    #[tokio::test]
    async fn stages_preserve_results_and_record_failures_and_cancellation() {
        let trace = Arc::new(Trace {
            id: "test".into(),
            started: Instant::now(),
            stages: Mutex::new(vec![]),
        });
        CURRENT
            .scope(trace.clone(), async {
                assert_eq!(
                    measure("positions", async { Ok::<_, &str>(42) }).await,
                    Ok(42)
                );
                assert_eq!(measure_sync("registry", || Err::<(), _>("bad")), Err("bad"));
                let pending = measure::<(), ()>("pending", std::future::pending());
                assert!(
                    tokio::time::timeout(std::time::Duration::ZERO, pending)
                        .await
                        .is_err()
                );
                mark("already_open");
            })
            .await;
        let rows = trace.stages.lock().unwrap();
        assert_eq!(rows.len(), 4);
        assert_eq!(rows[0]["outcome"], "ok");
        assert_eq!(rows[1]["outcome"], "error");
        assert_eq!(rows[2]["outcome"], "interrupted");
        assert_eq!(rows[3]["stage"], "already_open");
        assert!(
            rows.iter()
                .all(|r| r["duration_ms"].as_f64().unwrap() >= 0.0)
        );
    }


    /// Verifies that concurrent loads do not mix stages.
    #[tokio::test]
    async fn concurrent_loads_do_not_mix_stages() {
        let make = || {
            Arc::new(Trace {
                id: uuid::Uuid::new_v4().to_string(),
                started: Instant::now(),
                stages: Mutex::new(vec![]),
            })
        };
        let a = make();
        let b = make();
        let (first, second) = tokio::join!(
            CURRENT.scope(a.clone(), measure("first", async { Ok::<_, ()>(()) })),
            CURRENT.scope(b.clone(), measure("second", async { Ok::<_, ()>(()) }))
        );
        assert!(first.is_ok() && second.is_ok());
        assert_eq!(a.stages.lock().unwrap()[0]["stage"], "first");
        assert_eq!(b.stages.lock().unwrap()[0]["stage"], "second");
    }
}
