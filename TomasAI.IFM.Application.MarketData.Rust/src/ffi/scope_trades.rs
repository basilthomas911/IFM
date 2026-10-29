//! Copied option publications with immutable source-time trade inputs. All
//! manifests are copied before their queued owner is removed; no native pointer
//! or application callback escapes this synchronous bounded drain.
use super::{
    live::NativeMarketEvent,
    publication::NativePublication,
    runtime::{Status, boundary, instance, valid},
    scopes,
};
use crate::{
    dataset::{lanes::Phase, live::OutputSource, records::Observation, transport::Lane},
    option_chain::qualification::Quote,
};

/// Complete 816-byte publication plus actual-trade preparation. It does not
/// contain pointers; binary manifest bytes reside in separate caller storage.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct NativeOptionPublication {
    /// Unchanged 656-byte source/derived-state layout.
    pub publication: NativePublication,
    /// DTO size, 816.
    pub size: u32,
    /// Captured=1, underlying=2, manifest=4, non-replay option trade=8.
    pub flags: u32,
    /// Frozen immutable pricing-context identity, low/high bits.
    pub context: [u64; 2],
    /// Captured source-time API valuation instant (Unix UTC ticks).
    pub at: i64,
    /// Qualified immutable reference boundary (Unix UTC ticks).
    pub qualified_until: i64,
    /// Exact underlying at trade source processing, meaningful by flags.
    pub underlying: Quote,
    /// Byte offset in the separate caller-owned manifest buffer.
    pub manifest_offset: u32,
    /// Exact binary manifest bytes at that offset, zero when unavailable.
    pub manifest_length: u32,
}
impl Default for NativeOptionPublication {
    fn default() -> Self {
        Self {
            publication: NativePublication::from_state(
                NativeMarketEvent::default(),
                Default::default(),
            ),
            size: 816,
            flags: 0,
            context: [0; 2],
            at: 0,
            qualified_until: 0,
            underlying: Quote::default(),
            manifest_offset: 0,
            manifest_length: 0,
        }
    }
}
/// Copies up to sixteen records, including a complete owned binary context for
/// each actual trade. Records that do not fit the manifest destination remain
/// queued. If the first record cannot fit, Capacity preserves all caller outputs.
/// An expected logical-generation mismatch returns zero counts, never new data.
/// # Safety
/// Scope has scope_length readable bytes. Output holds capacity aligned 816-byte
/// records. Manifests holds manifest_capacity writable bytes. Count and written
/// are aligned exclusive u32 destinations; all caller buffers/scalars are disjoint.
/// Null data pointers are permitted only for their respective zero capacities.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_scope_publications_v2(
    handle: u64,
    scope: *const u8,
    scope_length: u32,
    expected_low: u64,
    expected_high: u64,
    size: u32,
    output: *mut NativeOptionPublication,
    capacity: u32,
    manifests: *mut u8,
    manifest_capacity: u32,
    count: *mut u32,
    written: *mut u32,
) -> i32 {
    boundary(|| {
        if !valid(count)
            || !valid(written)
            || capacity > 16
            || (capacity > 0 && !valid(output))
            || manifest_capacity > 4 * 1024 * 1024
            || (manifest_capacity > 0 && manifests.is_null())
        {
            return Err(Status::InvalidInput);
        }
        if size != 816 {
            return Err(Status::AbiMismatch);
        }
        // SAFETY: Scope is borrowed only during this bounded call.
        let id = unsafe { scopes::name(scope, scope_length)? };
        let instance = instance(handle)?;
        let expected = u128::from(expected_low) | (u128::from(expected_high) << 64);
        let owner = scopes::owner(&instance.scopes)?;
        let data = match &owner {
            Some(owner)
                if owner.generation() == expected
                    && owner
                        .status(id)
                        .map_err(|_| Status::InternalFailure)?
                        .is_some_and(|s| s.phase == Phase::Running) =>
            {
                owner.scope(id).map_err(|_| Status::InternalFailure)?
            }
            _ => None,
        };
        let mut copied = 0u32;
        let mut used = 0u32;
        if let Some(data) = data {
            while copied < capacity {
                let mut insufficient = false;
                let row=data.consume_output(Lane::OptionsGlbx,|record|{
                    let manifest=record.trade.as_ref().and_then(|trade|trade.prepared.manifest.as_ref());
                    let bytes=manifest.map_or(&[][..],|manifest|manifest.bytes());
                    if bytes.len()>manifest_capacity as usize-used as usize{insufficient=true;return Ok(None);}

                    let mut row=NativeOptionPublication{publication:NativePublication::from_output(record),size:816,..Default::default()};
                    if matches!(record.source,OutputSource::Observation(Observation::Trade(trade))if trade.header.flags&2==0)&&record.state.option_statistics.is_some(){row.flags|=8;}
                    if let Some(trade)=&record.trade {
                        row.flags|=1;row.context=[trade.prepared.context.identity as u64,(trade.prepared.context.identity>>64)as u64];
                        row.at=trade.at;row.qualified_until=trade.prepared.context.valid_until;
                        if let Some(underlying)=trade.underlying{row.flags|=2;row.underlying=underlying;}
                        if manifest.is_some(){row.flags|=4;row.manifest_offset=used;row.manifest_length=bytes.len()as u32;
                            // SAFETY: Remaining capacity checked before any write; buffers are caller-owned and disjoint.
                            unsafe{std::ptr::copy_nonoverlapping(bytes.as_ptr(),manifests.add(used as usize),bytes.len());}
                        }
                    }
                    Ok(Some(row))
                }).map_err(|_|Status::InternalFailure)?;
                let Some(row) = row else {
                    if insufficient && copied == 0 {
                        return Err(Status::Capacity);
                    }
                    break;
                };
                used += row.manifest_length;
                // SAFETY: copied < capacity and output alignment/range are validated by the caller contract.
                unsafe {
                    output.add(copied as usize).write(row);
                }
                copied += 1;
            }
        }
        // SAFETY: Independent aligned exclusive scalars, validated before any consumption.
        unsafe {
            count.write(copied);
            written.write(used);
        }
        Ok(())
    })
}
