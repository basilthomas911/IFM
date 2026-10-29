//! Viewport-sized console retention. No transport I/O or durable storage.
use std::sync::atomic::{AtomicUsize, Ordering};
// Two bootstrap entries until the first layout measures the actual viewport.
static CAPACITY: AtomicUsize = AtomicUsize::new(2);
/// Count the partially visible final row and retain exactly two visible pages.
pub fn for_viewport(height:f32, row_pitch:f32)->usize {
    if !height.is_finite() || !row_pitch.is_finite() || row_pitch<=0. {return 2;}
    ((height.max(0.)/row_pitch).ceil() as usize).max(1).saturating_mul(2)
}
/// Only resize changes the shared retention target; normal frames do no writes.
pub fn observe(height:f32,row_pitch:f32)->usize {
    let capacity=for_viewport(height,row_pitch);
    if CAPACITY.load(Ordering::Relaxed)!=capacity {CAPACITY.store(capacity,Ordering::Relaxed);}
    capacity
}
pub fn current()->usize {CAPACITY.load(Ordering::Relaxed)}

/// Evict oldest entries while preserving the one current warning. A resize
/// drops rows in one pass rather than repeatedly shifting the whole list.
pub fn trim_to(rows:&mut Vec<crate::sidebar::StatusRow>,capacity:usize) {
    let mut excess=rows.len().saturating_sub(capacity.max(2));
    rows.retain(|row| {
        if excess>0 && !row.id.starts_with("current-warning|") {excess-=1;false}else{true}
    });
}
