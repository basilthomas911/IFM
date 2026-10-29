//! Fixed-width source plus derived-state publication. Both are captured under
//! the book writer, not assembled by reading a later mutable cache after drain.
use super::{
    live::{NativeMarketEvent, empty_output, live},
    runtime::{Status, boundary, valid},
};
use crate::dataset::book::{PublicationState, Update};

/// Source and its coherent aggregate state. The hot ABI contains only integers;
/// authoritative decimal coefficients never pass through binary floating point.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct NativePublication {
    /// Source record, including lane, generation and provider identity.
    pub source: NativeMarketEvent,
    /// Total DTO bytes, currently 656.
    pub size: u32,
    /// Presence: market=1, quote=2, trade=4, session=8, VWAP=16, option stats=32.
    pub flags: u32,
    /// Update: unchanged=0, quote=1, trade-suppressed=2, trade-published=3,
    /// stats=4, trade-replay-complete=5, statistics-replay-complete=6.
    pub update: u32,
    /// Always zero.
    pub reserved: u32,
    /// Fixed exact slots documented in ABI-OWNERSHIP.md; unused slots are zero.
    pub fields: [i64; 64],
}
fn date(value: time::Date) -> i64 {
    i64::from(value.year()) * 10_000
        + i64::from(u8::from(value.month())) * 100
        + i64::from(value.day())
}
impl NativePublication {
    /// Copies a queued source contribution with its source-time API observation
    /// instant in slot 63. Non-consuming state reads leave that slot zero.
    pub fn from_output(output: &crate::dataset::live::Output) -> Self {
        let mut value = Self::from_state(output.into(), output.state);
        value.fields[63] = output.observed;
        value
    }
    /// Copies immutable native values without allocation or managed callbacks.
    pub fn from_state(source: NativeMarketEvent, state: PublicationState) -> Self {
        let mut result = Self {
            source,
            size: 656,
            flags: 0,
            update: if matches!(source.kind, 5 | 6) {
                source.kind
            } else {
                match state.update {
                    None | Some(Update::Unchanged) => 0,
                    Some(Update::Quote) => 1,
                    Some(Update::Trade {
                        price_changed: false,
                    }) => 2,
                    Some(Update::Trade {
                        price_changed: true,
                    }) => 3,
                    Some(Update::Statistics(_)) => 4,
                }
            },
            reserved: 0,
            fields: [0; 64],
        };
        let f = &mut result.fields;
        if let Some(market) = state.market {
            result.flags |= 1;
            f[0] = date(market.date);
            if let Some(q) = market.quote {
                result.flags |= 2;
                f[1..14].copy_from_slice(&[
                    q.sequence,
                    q.event_ticks,
                    q.receive_ticks,
                    q.value.local_received_ticks,
                    i64::from(q.value.bid.is_some()),
                    q.value.bid.unwrap_or(0),
                    i64::from(q.value.bid_size),
                    i64::from(q.value.bid_count),
                    i64::from(q.value.ask.is_some()),
                    q.value.ask.unwrap_or(0),
                    i64::from(q.value.ask_size),
                    i64::from(q.value.ask_count),
                    0,
                ]);
            }
            if let Some(t) = market.trade {
                result.flags |= 4;
                f[14..25].copy_from_slice(&[
                    t.sequence,
                    t.event_ticks,
                    t.receive_ticks,
                    t.value.value.price,
                    i64::from(t.value.value.size),
                    i64::from(t.value.action),
                    i64::from(t.value.side),
                    i64::from(t.value.conditions),
                    t.value.epoch as u64 as i64,
                    (t.value.epoch >> 64) as u64 as i64,
                    t.value.ordinal,
                ]);
            }
        }
        if let Some(s) = state.session {
            result.flags |= 8;
            f[25..33].copy_from_slice(&[
                date(s.value_date),
                s.open,
                s.high,
                s.low,
                i64::from(s.sequence),
                s.event_ns,
                s.volume,
                s.quality as i64,
            ]);
        }
        if let Some(v) = state.vwap {
            result.flags |= 16;
            f[33..52].copy_from_slice(&[
                v.epoch as u64 as i64,
                (v.epoch >> 64) as u64 as i64,
                v.ordinal,
                v.sequence,
                v.at,
                v.price_volume.coefficient as u64 as i64,
                (v.price_volume.coefficient >> 64) as u64 as i64,
                i64::from(v.price_volume.scale),
                v.volume,
                v.eligible,
                v.rejected,
                v.last_price.coefficient as u64 as i64,
                (v.last_price.coefficient >> 64) as u64 as i64,
                i64::from(v.last_price.scale),
                i64::from(v.valid),
                i64::from(v.replay_complete),
                v.generation as u64 as i64,
                (v.generation >> 64) as u64 as i64,
                v.reason as i64,
            ]);
        }
        if let Some(s) = state.option_statistics {
            result.flags |= 32;
            f[52] = i64::from(s.volume.is_some())
                | (i64::from(s.open_interest.is_some()) << 1)
                | (i64::from(s.statistics_at.is_some()) << 2)
                | (i64::from(s.volume_date.is_some()) << 3)
                | (i64::from(s.open_interest_date.is_some()) << 4)
                | (i64::from(s.volume_at.is_some()) << 5)
                | (i64::from(s.open_interest_at.is_some()) << 6)
                | (i64::from(s.official) << 7)
                | (i64::from(s.replay_complete) << 8);
            f[53] = s.volume.unwrap_or(0);
            f[54] = s.open_interest.unwrap_or(0);
            f[55] = s.statistics_at.unwrap_or(0);
            f[56] = s.volume_date.map_or(0, date);
            f[57] = s.open_interest_date.map_or(0, date);
            f[58] = s.volume_at.unwrap_or(0);
            f[59] = s.open_interest_at.unwrap_or(0);
            f[60] = s.observed;
            f[61] = s.last_event_ns;
            f[62] = i64::from(s.last_sequence);
        }
        result
    }
}
/// Drains up to sixteen source+state publications without per-record allocation.
/// This and raw events drain the same FIFO; a consumer chooses one representation.
/// # Safety
/// Output holds capacity aligned exclusive records; count is an independent
/// aligned exclusive u32. Null output is permitted only for zero capacity.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_live_publications_v1(
    handle: u64,
    output: *mut NativePublication,
    capacity: u32,
    count: *mut u32,
) -> i32 {
    boundary(|| {
        if !valid(count) || capacity > 16 || (capacity != 0 && !valid(output)) {
            return Err(Status::InvalidInput);
        }
        let mut batch: [_; 16] = std::array::from_fn(|_| empty_output());
        let copied = live(handle)?
            .drain_output(&mut batch[..capacity as usize])
            .map_err(|_| Status::InternalFailure)?;
        for (index, value) in batch[..copied].iter().enumerate() {
            let publication = NativePublication::from_output(value);
            // SAFETY: Copy count fits validated caller capacity; no pointers retained.
            unsafe { output.add(index).write(publication) };
        }
        // SAFETY: Count is aligned writable and independent from the record buffer.
        unsafe { count.write(copied as u32) };
        Ok(())
    })
}

/// Immediate non-consuming derived-state query. Source kind zero identifies a
/// query result, not a fabricated provider event. Publisher is the bootstrap-
/// qualified identity supplied by the application contract mapping. Unready or
/// stopped lanes succeed with no presence flags; errors preserve caller memory.
/// # Safety
/// Output points to one aligned exclusively writable 656-byte publication DTO.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_live_state_v1(
    handle: u64,
    selected: u32,
    instrument: u32,
    publisher: u32,
    size: u32,
    output: *mut NativePublication,
) -> i32 {
    boundary(|| {
        if !valid(output) || instrument == 0 || publisher > u16::MAX.into() {
            return Err(Status::InvalidInput);
        }
        if size != 656 {
            return Err(Status::AbiMismatch);
        }
        let lane = super::live::lane(selected)?;
        let mut source = NativeMarketEvent {
            size: 128,
            lane: selected,
            instrument,
            ..Default::default()
        };
        source.fields[4] = i64::from(publisher);
        let state = live(handle)?
            .publication_state(lane, instrument)
            .map_err(|_| Status::InternalFailure)?;
        let value = if let Some((generation, state)) = state {
            source.generation_low = generation as u64;
            source.generation_high = (generation >> 64) as u64;
            NativePublication::from_state(source, state)
        } else {
            NativePublication::from_state(source, Default::default())
        };
        // SAFETY: Validated aligned output; caller owns one complete DTO.
        unsafe { output.write(value) };
        Ok(())
    })
}
