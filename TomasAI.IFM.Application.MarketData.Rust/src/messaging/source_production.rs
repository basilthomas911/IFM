//! Opt-in async production boundary. Existing accepted state remains owned;
//! source forwarding and its receipt settlement are deliberately not paused.
use std::sync::{Arc,Condvar,Mutex,OnceLock};
use std::time::{Duration,Instant};
use tokio::sync::Notify;
use tokio_util::sync::CancellationToken;

#[derive(Default)]
struct State{lease:u64,active:u64,sealed:bool}
#[derive(Default)]
struct Gate{state:Mutex<State>,changed:Condvar,resumed:Notify}
static GATE:OnceLock<Arc<Gate>>=OnceLock::new();
fn gate()->Arc<Gate>{GATE.get_or_init(||Arc::new(Gate::default())).clone()}
#[cfg(test)]
pub(crate) fn pause_for_test(lease:u64,budget:Duration)->i32{gate().pause(lease,budget)}
#[cfg(test)]
pub(crate) fn resume_for_test(lease:u64)->i32{gate().resume(lease)}

pub(crate) struct ProductionLease{gate:Arc<Gate>}
impl Drop for ProductionLease{
    fn drop(&mut self){if let Ok(mut state)=self.gate.state.lock(){state.active=state.active.saturating_sub(1);self.gate.changed.notify_all();}}
}
async fn enter_gate(gate:Arc<Gate>,stop:&CancellationToken)->Result<ProductionLease,String>{
    enter_mode(gate,stop,false).await
}
async fn enter_mode(gate:Arc<Gate>,stop:&CancellationToken,accepted:bool)->Result<ProductionLease,String>{
    loop{
        let notified=gate.resumed.notified();tokio::pin!(notified);notified.as_mut().enable();
        {
            let mut state=gate.state.lock().map_err(|_|"Native production boundary interrupted")?;
            if state.lease==0||accepted&&!state.sealed{state.active=state.active.checked_add(1).ok_or("Native production admission overflow")?;drop(state);return Ok(ProductionLease{gate:gate.clone()});}
        }
        tokio::select!{biased;()=stop.cancelled()=>return Err("Native production admission cancelled".into()),()=&mut notified=>{}}
    }
}
/// Hold through one coherent accepted mutation, including required async work.
/// Never nest permits or hold one across an idle scheduler/next-record wait.
pub(crate) async fn enter(stop:&CancellationToken)->Result<Option<ProductionLease>,String>{
    if !crate::checkpoint::enabled(){return Ok(None);}enter_gate(gate(),stop).await.map(Some)
}
/// Drains already accepted owner facts during pause preparation. New timer/provider
/// work must use `enter`; after a successful owner prepare this path is sealed too.
pub(crate) async fn enter_accepted(stop:&CancellationToken)->Result<Option<ProductionLease>,String>{
    if !crate::checkpoint::enabled(){return Ok(None);}enter_mode(gate(),stop,true).await.map(Some)
}
pub(crate) fn seal_prepared(lease:u64)->Result<(),String>{
    let gate=gate();let mut state=gate.state.lock().map_err(|_|"Native production boundary interrupted")?;
    if lease==0||state.lease!=lease||state.active!=0{return Err("Accepted native owner work has not finished its pause preparation".into());}
    state.sealed=true;Ok(())
}
impl Gate{
    fn pause(&self,lease:u64,budget:Duration)->i32{
        if lease==0||budget.is_zero()||budget>Duration::from_secs(30){return 1;}
        let until=Instant::now()+budget;let mut state=match self.state.lock(){Ok(value)=>value,Err(_)=>return 3};
        if state.lease!=0&&state.lease!=lease{return 4;}if state.lease==0{state.sealed=false;}state.lease=lease;
        while state.active!=0{
            let remaining=until.saturating_duration_since(Instant::now());if remaining.is_zero(){return 5;}
            state=match self.changed.wait_timeout(state,remaining){Ok((state,_))=>state,Err(_)=>return 3};
        }0
    }
    fn resume(&self,lease:u64)->i32{
        let mut state=match self.state.lock(){Ok(value)=>value,Err(_)=>return 3};
        if lease==0||state.lease!=lease{return 4;}state.lease=0;state.sealed=false;self.resumed.notify_waiters();0
    }
}

/// Pauses source mutation, joins accepted callbacks and flushes accepted owner buffers.
/// # Safety
/// `scope` must address `scope_length` readable UTF-8 bytes for this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_source_production_pause_v1(scope:*const u8,scope_length:u32,lease:u64,budget_ms:u32)->i32{
    if scope.is_null()||scope_length==0||scope_length>128{return 1;}
    if !crate::checkpoint::enabled()||!super::source_outbox::enabled(){return 6;}
    let scope=match std::str::from_utf8(unsafe{std::slice::from_raw_parts(scope,scope_length as usize)}){Ok(value)=>value,Err(_)=>return 1};
    let result=gate().pause(lease,Duration::from_millis(u64::from(budget_ms)));if result!=0{return result;}
    if crate::checkpoint::prepare_paused_production(scope,lease).is_err(){return 7;}0
}
/// Releases only this production owner's pause. It never releases forwarding.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_source_production_resume_v1(lease:u64)->i32{gate().resume(lease)}
/// Current production owner, zero if open; maximum on interrupted state.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_source_production_lease_v1()->u64{gate().state.lock().map(|state|state.lease).unwrap_or(u64::MAX)}
/// Accepted mutations still completing. This is not socket-received input count.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_source_production_active_v1()->u64{gate().state.lock().map(|state|state.active).unwrap_or(u64::MAX)}

#[cfg(test)]
mod tests{
    use super::*;
    #[tokio::test(flavor="multi_thread",worker_threads=2)]
    async fn pause_joins_actual_callback_and_waiters_yield_while_forwarding_can_continue(){
        let gate=Arc::new(Gate::default());let token=CancellationToken::new();let accepted=enter_gate(gate.clone(),&token).await.unwrap();
        let pause_gate=gate.clone();let paused=tokio::task::spawn_blocking(move||pause_gate.pause(91,Duration::from_secs(2)));
        while gate.state.lock().unwrap().lease==0{tokio::task::yield_now().await;}assert!(!paused.is_finished());
        let wait_gate=gate.clone();let wait_token=token.clone();let waiting=tokio::spawn(async move{enter_gate(wait_gate,&wait_token).await});
        drop(accepted);assert_eq!(paused.await.unwrap(),0);assert_eq!(gate.state.lock().unwrap().active,0);assert!(!waiting.is_finished());
        // Both runtime workers remain usable while source callbacks are asynchronously paused.
        assert_eq!(tokio::time::timeout(Duration::from_secs(1),tokio::spawn(async{73})).await.unwrap().unwrap(),73);
        assert_eq!(gate.resume(92),4);assert!(!waiting.is_finished());assert_eq!(gate.resume(91),0);drop(waiting.await.unwrap().unwrap());
    }
    #[tokio::test]
    async fn timeout_retains_original_pause_and_cancelled_waiter_cannot_mutate(){
        let gate=Arc::new(Gate::default());let token=CancellationToken::new();let accepted=enter_gate(gate.clone(),&token).await.unwrap();
        assert_eq!(gate.pause(93,Duration::from_millis(1)),5);drop(accepted);
        token.cancel();assert!(enter_gate(gate.clone(),&token).await.is_err());assert_eq!(gate.state.lock().unwrap().lease,93);
        assert_eq!(gate.resume(94),4);assert_eq!(gate.resume(93),0);
    }
}
