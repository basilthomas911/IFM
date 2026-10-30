//! Native setup-only underlying quotes. The temporary session belongs to the
//! options dataset parent, is joined before the final option session opens, and
//! shares the original dataset deadline. No lease or renewal crosses the FFI.
use super::{
    live::{LiveData, Start},
    transport::Lane,
};
use crate::{
    latest::{Asset, QuoteValue, Tick},
    option_chain::qualification::Decimal,
    reference::decimal_arithmetic as math,
    runtime::{Deadline, clock::Clock, owner::Child},
};
use std::{collections::BTreeMap, sync::Arc};
use tokio_util::sync::CancellationToken;
/// Existing background quote qualification: positive ordered sides, not future,
/// at most fifteen minutes old, exact bid/2 + ask/2 decimal midpoint.
pub fn midpoint(quote: Tick<QuoteValue>, now: i64) -> Option<Decimal> {
    let (bid, ask) = (quote.value.bid?, quote.value.ask?);
    if bid <= 0
        || ask < bid
        || quote.event_ticks > now
        || now.checked_sub(quote.event_ticks)? > 9_000_000_000
    {
        return None;
    }
    // A nanounit half is n*5 / 10^10, exactly representable within 96 bits.
    // Normalize each quotient as CLR decimal division does before adding them.
    let half = |n: i64| {
        let mut coefficient = (n as u128) * 5;
        let mut scale = 10u32;
        while scale != 0 && coefficient.is_multiple_of(10) {
            coefficient /= 10;
            scale -= 1;
        }
        Decimal {
            bits: [
                coefficient as u32,
                (coefficient >> 32) as u32,
                (coefficient >> 64) as u32,
                scale << 16,
            ],
        }
    };
    math::add(half(bid), half(ask))
}
struct Retirement(Arc<LiveData>);
impl Drop for Retirement {
    fn drop(&mut self) {
        let _ = self.0.cancel_lane(Lane::OptionsGlbx);
    }
}
/// Acquires every exact dependency without publishing temporary startup prices.
/// Returned decimals are detached values and cannot retain the source session.
///
/// # Errors
/// Invalid/duplicate dependencies, native source failure, cancellation/deadline,
/// or retirement that did not finish under the same original dataset budget.
pub async fn acquire(
    source: Start,
    date: time::Date,
    clock: Arc<Clock>,
    parent: Arc<Child>,
    stop: &CancellationToken,
    deadline: Deadline,
) -> Result<BTreeMap<String, Decimal>, String> {
    acquire_with_progress(source,date,clock,parent,stop,deadline,|_|Ok(())).await
}
// The progress observer receives copied contract names only. It cannot own or
// delay the source socket, and is called only when the missing set changes.
pub(super) async fn acquire_with_progress(
    source: Start, date: time::Date, clock: Arc<Clock>, parent: Arc<Child>,
    stop: &CancellationToken, deadline: Deadline,
    progress: impl Fn(&[String])->Result<(),String> + Send,
) -> Result<BTreeMap<String, Decimal>, String> {
    collect(source,date,clock,parent,stop,deadline,progress,|_|Ok(()),true).await
}
/// Observe qualified startup dependencies continuously without gating a dataset.
/// Snapshots contain source timestamps so callers revalidate freshness on use.
#[allow(clippy::too_many_arguments)]
pub(super) async fn observe(
    source: Start, date: time::Date, clock: Arc<Clock>, parent: Arc<Child>,
    stop: &CancellationToken, deadline: Deadline,
    progress: impl Fn(&[String])->Result<(),String> + Send,
    snapshot: impl Fn(BTreeMap<String, Tick<QuoteValue>>)->Result<(),String> + Send,
) -> Result<BTreeMap<String, Decimal>, String> {
    collect(source,date,clock,parent,stop,deadline,progress,snapshot,false).await
}
#[allow(clippy::too_many_arguments)]
async fn collect(
    mut source: Start, date: time::Date, clock: Arc<Clock>, parent: Arc<Child>,
    stop: &CancellationToken, deadline: Deadline,
    progress: impl Fn(&[String])->Result<(),String> + Send,
    snapshot: impl Fn(BTreeMap<String, Tick<QuoteValue>>)->Result<(),String> + Send,
    finish_when_complete: bool,
) -> Result<BTreeMap<String, Decimal>, String> {
    if source.plan.lane != Lane::OptionsGlbx
        || source.registrations.is_empty()
        || source.registrations.len() > 64
        || source
            .registrations
            .iter()
            .any(|r| r.asset != Asset::Futures)
    {
        return Err("Invalid native background underlying dependencies".into());
    }
    // Only subscription connectivity gates this setup call. The quote loop below
    // independently waits for actual values and never fabricates them.
    source.evidence = super::live::Evidence::ControlOnly;
    let ids = source.registrations.clone();
    let data = Arc::new(LiveData::new(
        date,
        rand::random::<u128>().max(1),
        64,
        clock.clone(),
        parent.owner(),
    )?);
    let retirement = Retirement(data.clone());
    let mut changed = data.updates();
    // Await quote data without treating a quiet, live connection as failure.
    // Source setup is bounded; its SDK continues reading/monitoring heartbeats.
    let result = async {
        data.start_in_parent(source, stop, deadline, &parent)
            .await?;
        let mut lifecycle = data
            .lifecycle(Lane::OptionsGlbx)?
            .ok_or("Native underlying lifecycle unavailable")?;
        let mut previous_missing=None;
        loop {
            let status = lifecycle.borrow_and_update().clone();
            if matches!(
                status.phase,
                super::lanes::Phase::Failed | super::lanes::Phase::Stopped
            ) {
                return Err(status
                    .error
                    .or(status.root_error)
                    .unwrap_or_else(|| "Native underlying source stopped".into()));
            }
            let now = clock
                .utc_ticks()
                .map_err(|_| "Native background clock unavailable")?;
            let queries = data
                .queries(Lane::OptionsGlbx)?
                .ok_or("Native underlying query unavailable")?;
            let mut prices = BTreeMap::new();
            let mut quotes = BTreeMap::new();
            for id in &ids {
                let quote = queries.raw_prices(id.instrument)?.and_then(|p| p.quote);
                if let Some(quote) = quote { quotes.insert(id.contract.clone(), quote); }
                if let Some(price) = quote.and_then(|q| midpoint(q, now))
                    && prices.insert(id.contract.clone(), price).is_some()
                {
                    return Err("Duplicate native underlying identity".into());
                }
            }
            snapshot(quotes)?;
            if finish_when_complete && prices.len() == ids.len() {
                return Ok(prices);
            }
            let missing=ids.iter().filter(|id|!prices.contains_key(&id.contract)).map(|id|id.contract.clone()).collect::<Vec<_>>();
            if previous_missing.as_ref()!=Some(&missing) {progress(&missing)?;previous_missing=Some(missing);}
            tokio::select! {
                result=changed.changed()=>result.map_err(|_|"Native underlying observation closed")?,
                result=lifecycle.changed()=>result.map_err(|_|"Native underlying lifecycle closed")?,
            }
        }
    };
    let result = tokio::select! {biased;()=stop.cancelled()=>Err("Underlying quote wait cancelled".into()),result=result=>result};
    data.cancel_lane(Lane::OptionsGlbx)?;
    let joined = data
        .stop(
            Lane::OptionsGlbx,
            &CancellationToken::new(),
            Deadline::recovery(),
        )
        .await;
    drop(retirement);
    match (result, joined) {
        (Ok(prices), Ok(())) => Ok(prices),
        (Err(error), _) | (_, Err(error)) => Err(error),
    }
}
