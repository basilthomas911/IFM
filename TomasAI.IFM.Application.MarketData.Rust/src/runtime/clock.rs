//! Explicit application-supplied business time, independent of recovery timers.
//! No local date, time zone, SystemTime or ValueDate inference occurs here.
use std::{sync::RwLock, time::Instant};
const MIN: i64 = -621355968000000000;
const MAX: i64 = 2534023007999999999;
/// Invalid timestamp or interrupted clock-writer invariant.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Failure {
    /// The source is outside the existing DateTimeOffset UTC range.
    OutOfRange,
    /// A previous panic interrupted the short clock update.
    InterruptedWriter,
}
struct State {
    ticks: i64,
    anchor: Option<Instant>,
}
/// A captured UTC anchor, or an explicitly controlled source. The application can
/// replace either mode without changing business dates inside dataset owners.
pub struct Clock {
    state: RwLock<State>,
    changes: tokio::sync::watch::Sender<u64>,
}
impl Clock {
    pub(crate) fn checkpoint(&self)->Result<rmpv::Value,String>{
        let state=self.state.read().map_err(|_|"Checkpoint clock owner interrupted")?;
        let elapsed=i64::try_from(state.anchor.map_or(0,|anchor|anchor.elapsed().as_nanos()/100)).map_err(|_|"Checkpoint clock elapsed overflow")?;
        let ticks=state.ticks.checked_add(elapsed).filter(|value|(MIN..=MAX).contains(value)).ok_or("Checkpoint clock out of range")?;
        Ok(rmpv::Value::Array(vec![1.into(),ticks.into(),state.anchor.is_some().into(),(*self.changes.borrow()).into()]))
    }
    pub(crate) fn restore_checkpoint_controlled(&self,value:&rmpv::Value)->Result<(),String>{
        let f=crate::checkpoint::fields(value,4)?;if crate::checkpoint::unsigned(&f[0])?!=1{return Err("Unknown checkpoint clock".into());}
        let ticks=crate::checkpoint::number(&f[1])?;crate::checkpoint::boolean(&f[2])?;let version=crate::checkpoint::unsigned(&f[3])?;
        if !(MIN..=MAX).contains(&ticks){return Err("Checkpoint original clock out of range".into());}
        *self.state.write().map_err(|_|"Checkpoint clock owner interrupted")?=State{ticks,anchor:None};self.changes.send_replace(version);Ok(())
    }
    /// Captures API UTC ticks since Unix epoch. Advancing mode extrapolates only
    /// elapsed time; controlled mode changes solely through explicit update calls.
    pub fn new(ticks: i64, advancing: bool) -> Result<Self, Failure> {
        if !(MIN..=MAX).contains(&ticks) {
            return Err(Failure::OutOfRange);
        }
        Ok(Self {
            changes: tokio::sync::watch::channel(0).0,
            state: RwLock::new(State {
                ticks,
                anchor: advancing.then(Instant::now),
            }),
        })
    }
    /// Replaces the API anchor, including deliberate simulated-clock movements.
    /// Callers reset active epochs when a replay seek invalidates source ordering.
    pub fn update(&self, ticks: i64, advancing: bool) -> Result<(), Failure> {
        let _checkpoint=crate::checkpoint::transaction().map_err(|_|Failure::InterruptedWriter)?;
        if !(MIN..=MAX).contains(&ticks) {
            return Err(Failure::OutOfRange);
        }
        *self.state.write().map_err(|_| Failure::InterruptedWriter)? = State {
            ticks,
            anchor: advancing.then(Instant::now),
        };
        self.changes
            .send_modify(|version| *version = version.saturating_add(1));
        Ok(())
    }
    /// Observes explicit API clock updates without a polling loop.
    pub fn changes(&self) -> tokio::sync::watch::Receiver<u64> {
        self.changes.subscribe()
    }
    /// Reads an exact controlled timestamp or its advancing elapsed-time value.
    /// Overflow is explicit; the clock never clamps to a plausible-looking date.
    pub fn utc_ticks(&self) -> Result<i64, Failure> {
        let state = self.state.read().map_err(|_| Failure::InterruptedWriter)?;
        let elapsed = state.anchor.map_or(0, |a| a.elapsed().as_nanos() / 100);
        let elapsed = i64::try_from(elapsed).map_err(|_| Failure::OutOfRange)?;
        state
            .ticks
            .checked_add(elapsed)
            .filter(|v| (MIN..=MAX).contains(v))
            .ok_or(Failure::OutOfRange)
    }
}
