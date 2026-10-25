//! Operational timings only: never use this clock for ValueDate or trading decisions.
//! One correlated JSON record per load, including failed/cancelled loads. Nested
//! durations overlap (notably parallel positions/limits); do not sum them.
use serde_json::{json, Value};
use std::{future::Future, sync::{Arc, Mutex}, time::Instant};

tokio::task_local! { static CURRENT: Arc<Trace>; }
struct Trace { id: String, started: Instant, stages: Mutex<Vec<Value>> }
struct Stage { trace: Arc<Trace>, name: &'static str, started: Instant, outcome: &'static str }
impl Stage {
    fn new(name: &'static str) -> Option<Self> {
        CURRENT.try_with(|trace| Self { trace: trace.clone(), name, started: Instant::now(), outcome: "interrupted" }).ok()
    }
}
impl Drop for Stage {
    fn drop(&mut self) {
        if let Ok(mut stages) = self.trace.stages.lock() {
            stages.push(json!({"stage": self.name, "start_ms": self.started.duration_since(self.trace.started).as_secs_f64()*1000.0,
                "duration_ms": self.started.elapsed().as_secs_f64()*1000.0, "outcome": self.outcome}));
        }
    }
}
/// Measure awaited backend work, including deserialization, without changing its result.
pub async fn measure<T,E>(name: &'static str, work: impl Future<Output=Result<T,E>>) -> Result<T,E> {
    let mut stage=Stage::new(name);
    let result=work.await;
    if let Some(stage)=&mut stage { stage.outcome=if result.is_ok(){"ok"}else{"error"}; }
    result
}
pub fn measure_sync<T,E>(name: &'static str, work: impl FnOnce()->Result<T,E>) -> Result<T,E> {
    let mut stage=Stage::new(name); let result=work();
    if let Some(stage)=&mut stage {stage.outcome=if result.is_ok(){"ok"}else{"error"};} result
}
pub fn mark(name: &'static str) {
    if let Some(mut stage)=Stage::new(name) {stage.outcome="ok";}
}
struct Completion { event: &'static str, sequence: u64, trace: Arc<Trace>, outcome: &'static str, selection: Value }
impl Completion {
    fn record(&self)->Value {
        json!({"event":self.event, "load_id":self.trace.id,"process_id":std::process::id(),"process_load_number":self.sequence,
            "observed_at_utc":chrono::Utc::now().to_rfc3339(),"source":if std::env::var_os("IFM_STANDALONE_TEST_NATS_URL").is_some(){"isolated-test"}else{"ui"},"selection":self.selection,
            "outcome":self.outcome,"total_ms":self.trace.started.elapsed().as_secs_f64()*1000.0,
            "stages":self.trace.stages.lock().map(|s|s.clone()).unwrap_or_default()})
    }
}
impl Drop for Completion {
    fn drop(&mut self) { write_record(&self.record()); }
}
/// Scope survives await points but not spawned tasks; all measured load stages
/// are awaited here. Dropping the deadline future records an interrupted load.
pub async fn run<T,E>(selection: Value, work: impl Future<Output=Result<T,E>>) -> Result<T,E> {
    let trace=Arc::new(Trace{id:uuid::Uuid::new_v4().to_string(),started:Instant::now(),stages:Mutex::new(Vec::new())});
    static LOAD_SEQUENCE:std::sync::atomic::AtomicU64=std::sync::atomic::AtomicU64::new(0);
    let sequence=LOAD_SEQUENCE.fetch_add(1,std::sync::atomic::Ordering::Relaxed)+1;
    let mut completion=Completion{event:"trade_load_timing",sequence,trace:trace.clone(),outcome:"interrupted",selection};
    let result=CURRENT.scope(trace,work).await;
    completion.outcome=if result.is_ok(){"ok"}else{"error"}; result
}
pub fn current_id()->Option<String>{CURRENT.try_with(|trace|trace.id.clone()).ok()}
/// Optional hydration is a separate duration; it is not part of foreground load latency.
pub async fn background<T,E>(selection:Value,work:impl Future<Output=Result<T,E>>)->Result<T,E>{
    let trace=Arc::new(Trace{id:uuid::Uuid::new_v4().to_string(),started:Instant::now(),stages:Mutex::new(Vec::new())});
    let mut completion=Completion{event:"trade_load_background_timing",sequence:0,trace:trace.clone(),outcome:"interrupted",selection};
    let result=CURRENT.scope(trace,work).await;
    completion.outcome=if result.is_ok(){"ok"}else{"error"};result
}
fn write_record(record:&Value) {
    // Serialize once per user load, not on market ticks. Failure to write a
    // diagnostic must never prevent opening a trade or alter recovery behavior.
    let line=record.to_string(); eprintln!("{line}");
    static FILE_LOCK:Mutex<()>=Mutex::new(());
    let Ok(_lock)=FILE_LOCK.lock() else{return};
    let Some(base)=std::env::var_os("LOCALAPPDATA") else{return};
    let directory=std::path::PathBuf::from(base).join("TomasAI/IFM/UI.Rust.eGui/Logs");
    let result=(||->std::io::Result<()> {
        use std::io::Write;
        std::fs::create_dir_all(&directory)?;
        // Per-process files avoid races between simultaneously running clients.
        let path=directory.join(format!("trade-load-{}.jsonl",std::process::id()));
        if std::fs::metadata(&path).map(|m|m.len()>5*1024*1024).unwrap_or(false) {
            let previous=path.with_extension("previous.jsonl");
            if previous.exists(){std::fs::remove_file(&previous)?;}
            std::fs::rename(&path,previous)?;
        }
        let mut output=std::fs::OpenOptions::new().create(true).append(true).open(path)?;
        writeln!(output,"{line}")
    })();
    if let Err(error)=result {eprintln!("Trade-load diagnostic write failed: {error}");}
}

#[cfg(test)]
mod tests {
    use super::*;
    #[tokio::test]
    async fn stages_preserve_results_and_record_failures_and_cancellation() {
        let trace=Arc::new(Trace{id:"test".into(),started:Instant::now(),stages:Mutex::new(vec![])});
        CURRENT.scope(trace.clone(),async {
            assert_eq!(measure("positions",async{Ok::<_,&str>(42)}).await,Ok(42));
            assert_eq!(measure_sync("registry",||Err::<(),_>("bad")),Err("bad"));
            let pending=measure::<(),()>("pending",std::future::pending());
            assert!(tokio::time::timeout(std::time::Duration::ZERO,pending).await.is_err());
            mark("already_open");
        }).await;
        let rows=trace.stages.lock().unwrap();
        assert_eq!(rows.len(),4);
        assert_eq!(rows[0]["outcome"],"ok");assert_eq!(rows[1]["outcome"],"error");
        assert_eq!(rows[2]["outcome"],"interrupted");assert_eq!(rows[3]["stage"],"already_open");
        assert!(rows.iter().all(|r|r["duration_ms"].as_f64().unwrap()>=0.0));
    }
    #[tokio::test]
    async fn concurrent_loads_do_not_mix_stages() {
        let make=||Arc::new(Trace{id:uuid::Uuid::new_v4().to_string(),started:Instant::now(),stages:Mutex::new(vec![])});
        let a=make();let b=make();
        let (first,second)=tokio::join!(CURRENT.scope(a.clone(),measure("first",async{Ok::<_,()>(())})),CURRENT.scope(b.clone(),measure("second",async{Ok::<_,()>(())})));
        assert!(first.is_ok()&&second.is_ok());
        assert_eq!(a.stages.lock().unwrap()[0]["stage"],"first");
        assert_eq!(b.stages.lock().unwrap()[0]["stage"],"second");
    }
}
