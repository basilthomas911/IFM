//! Generation-qualified option-scope control. Every command shares the retained
//! control-result protocol and the caller's original remaining recovery budget.
//! Emergency stop signals the physical subtree before waiting on replacement.
use super::{
    live::{NativeControlResult, NativeLaneStatus, NativeMarketEvent, empty_output},
    publication::NativePublication,
    runtime::{Status, boundary, instance, valid},
};
use crate::{
    dataset::{lanes::Phase, live::LiveData, transport::Lane},
    option_chain::owner::Options,
    runtime::owner::{Outcome, foreign_deadline},
};
use std::sync::{Arc, Mutex};
use tokio_util::sync::CancellationToken;

/// Signals only the matching options subtree synchronously. This operation
/// bypasses command/completion capacity and requires no remaining time budget.
/// A stale generation is an idempotent no-op. Success means cancellation was
/// signaled, never that tasks/resources joined; use awaited owner replacement
/// or shutdown for cleanup proof. Futures and the cold/reference owner remain live.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_scope_stop_signal_v1(handle: u64, low: u64, high: u64) -> i32 {
    boundary(|| {
        let instance = instance(handle)?;
        let expected = u128::from(low) | (u128::from(high) << 64);
        if let Some(current) = owner(&instance.scopes)?
            && current.generation() == expected
        {
            current.request_stop();
        }
        Ok(())
    })
}

#[derive(Default)]
pub(super) struct Scopes {
    owner: Mutex<Option<Arc<Options>>>,
    pub(super) replacement: tokio::sync::Mutex<()>,
}
impl Scopes {
    pub(super) fn flat_available(&self) -> Result<bool, Status> {
        Ok(owner(self)?.is_none_or(|owner| owner.is_stopped()))
    }
}
pub(super) fn owner(scopes: &Scopes) -> Result<Option<Arc<Options>>, Status> {
    Ok(scopes
        .owner
        .lock()
        .map_err(|_| Status::InternalFailure)?
        .clone())
}
pub(super) unsafe fn name<'input>(input: *const u8, length: u32) -> Result<&'input str, Status> {
    if input.is_null() || length == 0 || length > 512 {
        return Err(Status::InvalidInput);
    }
    // SAFETY: Export requires length readable bytes; bounded before borrowing.
    let value = std::str::from_utf8(unsafe { std::slice::from_raw_parts(input, length as usize) })
        .map_err(|_| Status::InvalidInput)?;
    if value.trim().is_empty() || value.encode_utf16().count() > 128 {
        return Err(Status::InvalidInput);
    }
    Ok(value)
}
fn data(handle: u64, id: &str) -> Result<Option<Arc<LiveData>>, Status> {
    let configured = instance(handle)?;
    owner(&configured.scopes)?
        .map(|owner| owner.scope(id).map_err(|_| Status::InternalFailure))
        .transpose()
        .map(Option::flatten)
}
/// Reads per-scope physical lifecycle/attempt/episode evidence. Logical owner
/// preparation is not reported as a running physical feed. Retiring data cannot
/// be labelled running merely because a previous transport status was cached.
/// # Safety
/// Scope is readable for scope_length; output is aligned exclusive 64-byte data.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_scope_status_v1(
    handle: u64,
    scope: *const u8,
    scope_length: u32,
    size: u32,
    output: *mut NativeLaneStatus,
) -> i32 {
    boundary(|| {
        if !valid(output) {
            return Err(Status::InvalidInput);
        }
        if size != 64 {
            return Err(Status::AbiMismatch);
        }
        // SAFETY: Bounded readable scope name supplied by caller.
        let id = unsafe { name(scope, scope_length)? };
        let mut value = NativeLaneStatus {
            size: 64,
            ..Default::default()
        };
        if let Some(owner) = owner(&instance(handle)?.scopes)? {
            let current = owner.scope(id).map_err(|_| Status::InternalFailure)?;
            if let Some(state) = owner.status(id).map_err(|_| Status::InternalFailure)? {
                value.phase = match state.phase {
                    Phase::Stopped => 1,
                    Phase::Starting => 2,
                    Phase::Running if current.is_some() => 3,
                    Phase::Running | Phase::Recovering => 4,
                    Phase::Failed => 5,
                };
                value.attempt = state.generation;
                value.episode = state.episode;
                value.attempts = state.attempts;
            }
            if let Some(current) = current
                && let Some(generation) = current
                    .generation(Lane::OptionsGlbx)
                    .map_err(|_| Status::InternalFailure)?
            {
                value.generation_low = generation as u64;
                value.generation_high = (generation >> 64) as u64;
            }
        }
        // SAFETY: Validated exclusive aligned caller destination.
        unsafe {
            output.write(value);
        }
        Ok(())
    })
}
/// Reads reliable supervisor/current scope failure independently of log drain.
/// Empty scope selects the logical supervisor. No provider message is fabricated.
/// # Safety
/// Optional scope is readable for scope_length bytes; output is aligned exclusive
/// writable storage for one 4112-byte result, independent of scope memory.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_scope_failure_v1(
    handle: u64,
    scope: *const u8,
    scope_length: u32,
    size: u32,
    output: *mut NativeControlResult,
) -> i32 {
    boundary(|| {
        if !valid(output) {
            return Err(Status::InvalidInput);
        }
        if size != 4112 {
            return Err(Status::AbiMismatch);
        }
        let owner = owner(&instance(handle)?.scopes)?;
        let mut error = owner
            .as_ref()
            .map(|owner| owner.terminal_error().map_err(|_| Status::InternalFailure))
            .transpose()?
            .flatten();
        if scope_length != 0 {
            // SAFETY: Bounded readable scope name supplied by the caller.
            let id = unsafe { name(scope, scope_length)? };
            if error.is_none() {
                error = owner
                    .map(|owner| owner.status(id).map_err(|_| Status::InternalFailure))
                    .transpose()?
                    .flatten()
                    .and_then(|status| status.error);
            }
        }
        let value = NativeControlResult::finished(error.map_or(Ok(()), Err));
        // SAFETY: Validated aligned exclusive result destination.
        unsafe {
            output.write(value);
        }
        Ok(())
    })
}
/// Reads the logical options generation without starting a worker or connection.
/// Zero means unprepared. Physical scope books have independent generations.
/// # Safety
/// Low/high are aligned exclusive, independent writable u64 scalars.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_scope_generation_v1(
    handle: u64,
    low: *mut u64,
    high: *mut u64,
) -> i32 {
    boundary(|| {
        if !valid(low) || !valid(high) {
            return Err(Status::InvalidInput);
        }
        let generation = owner(&instance(handle)?.scopes)?.map_or(0, |owner| owner.generation());
        // SAFETY: Validated aligned independent writable caller scalars.
        unsafe {
            low.write(generation as u64);
            high.write((generation >> 64) as u64);
        }
        Ok(())
    })
}
/// Submits 0 install, 1 reset scope, 2 release scope, 3 stop options subtree,
/// 4 prepare/replace options owner, 5 atomic qualified-context refresh,
/// 6 complete context-fenced business risk demand; 7 native reviewed-reference install.
/// Cold JSON carries scope/digest/request and optional pricing for 0,
/// scope for 1/2, and {} for 3/4. All calls require the expected logical generation
/// (zero only for first preparation). Stale releases/stops cannot affect a newer
/// owner. Taking the standard terminal control result remains mandatory.
/// # Safety
/// Input contains length readable bytes (1..8 MiB). No caller memory is retained.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_scope_submit_v1(
    handle: u64,
    operation: u64,
    action: u32,
    expected_low: u64,
    expected_high: u64,
    input: *const u8,
    length: u32,
    budget_ms: u64,
) -> i32 {
    let deadline = foreign_deadline(budget_ms);
    boundary(|| {
        if action > 7 || input.is_null() || length == 0 || length > 8 * 1024 * 1024 {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Caller-readable bounded cold request; all content is copied.
        let value: serde_json::Value =
            serde_json::from_slice(unsafe { std::slice::from_raw_parts(input, length as usize) })
                .map_err(|_| Status::InvalidInput)?;
        if !value.is_object() {
            return Err(Status::InvalidInput);
        }
        let id = if action <= 2 || matches!(action, 5..=7) {
            value["scope"]
                .as_str()
                .filter(|id| {
                    !id.trim().is_empty() && id.len() <= 512 && id.encode_utf16().count() <= 128
                })
                .ok_or(Status::InvalidInput)?
                .to_owned()
        } else {
            String::new()
        };
        let digest = if matches!(action, 0 | 7) {
            value["digest"]
                .as_str()
                .filter(|digest| {
                    digest.len() == 64 && digest.bytes().all(|b| b.is_ascii_hexdigit())
                })
                .ok_or(Status::InvalidInput)?
                .to_owned()
        } else {
            String::new()
        };
        let instance = instance(handle)?;
        let request = if matches!(action, 0 | 7) {
            let mut bytes =
                serde_json::to_vec(&value["request"]).map_err(|_| Status::InvalidInput)?;
            let result = super::live::parse(&bytes, Lane::OptionsGlbx, instance.dns_slots.clone());
            bytes.fill(0);
            Some(result?)
        } else {
            None
        };
        let pricing = if action == 0 && !value["pricing"].is_null() {
            Some(super::scope_pricing::configuration(
                &value["pricing"],
                &instance.manifests,
            )?)
        } else {
            None
        };
        let refresh = if action == 5 {
            Some(super::scope_pricing::refresh(
                &value["pricing"],
                &instance.manifests,
            )?)
        } else {
            None
        };
        let risk = if action == 6 {
            Some(super::scope_pricing::risk_requirements(&value["pricing"])?)
        } else {
            None
        };
        let (clock, date, identity, flat) = {
            let configured = instance.live.lock().map_err(|_| Status::InternalFailure)?;
            let configured = configured.as_ref().ok_or(Status::InvalidInput)?;
            (
                configured.clock.clone(),
                configured.date,
                configured.identity,
                configured.live.clone(),
            )
        };
        let reviewed = if action == 7 {
            Some(super::reviewed::parse(&value["reviewed"], &instance)?)
        } else {
            None
        };
        let scopes = instance.scopes.clone();
        let root = instance.owner.clone();
        let expected = u128::from(expected_low) | (u128::from(expected_high) << 64);
        let mut reservation = instance.reserve_control(operation)?;
        let work = async move {
            let token = CancellationToken::new();
            let result = async {
                let prior =
                    owner(&scopes).map_err(|error| format!("Options owner state: {error:?}"))?;
                let actual = prior.as_ref().map_or(0, |owner| owner.generation());
                if expected != actual {
                    if matches!(action, 2 | 3) {
                        return Ok(());
                    }
                    return Err("Options owner generation changed".into());
                }
                if action == 3 {
                    if let Some(prior) = &prior {
                        prior.request_stop();
                    }
                    let _replacement = deadline
                        .run(&token, scopes.replacement.lock())
                        .await
                        .map_err(|error| error.to_string())?;
                    if let Some(current) = owner(&scopes).map_err(|error| format!("{error:?}"))?
                        && current.generation() == expected
                    {
                        current.shutdown(&token, deadline).await?;
                    }
                    return Ok(());
                }
                if action == 4 {
                    let _replacement = deadline
                        .run(&token, scopes.replacement.lock())
                        .await
                        .map_err(|error| error.to_string())?;
                    let current = owner(&scopes).map_err(|error| format!("{error:?}"))?;
                    if current.as_ref().map_or(0, |owner| owner.generation()) != expected {
                        return Err("Options owner generation changed".into());
                    }
                    if let Some(current) = current {
                        current.shutdown(&token, deadline).await?;
                    }
                    if flat
                        .status(Lane::OptionsGlbx)?
                        .is_some_and(|status| status.phase != Phase::Stopped)
                    {
                        return Err(
                            "Flat options lane must be stopped before scoped ownership".into()
                        );
                    }
                    let next = Arc::new(Options::with_diagnostics(
                        date,
                        identity,
                        clock,
                        root,
                        flat.diagnostic_sink(),
                    )?);
                    *scopes
                        .owner
                        .lock()
                        .map_err(|_| "Options owner state interrupted")? = Some(next);
                    return Ok(());
                }
                let Some(current) = prior else {
                    return if action == 2 {
                        Ok(())
                    } else {
                        Err("Options owner is not prepared".into())
                    };
                };
                let result = match action {
                    0 => {
                        let request = request.expect("validated scoped start");
                        if let Some(pricing) = pricing {
                            current
                                .install_priced(&id, &digest, request, pricing, &token, deadline)
                                .await
                        } else {
                            current
                                .install(&id, &digest, request, &token, deadline)
                                .await
                        }
                    }
                    7 => {
                        Box::pin(current.install_reviewed(
                            &id,
                            &digest,
                            request.expect("validated reviewed start"),
                            reviewed.expect("validated native references"),
                            &token,
                            deadline,
                        ))
                        .await
                    }
                    1 => current.reset(&id, &token, deadline).await,
                    5 => {
                        let prepared = refresh.ok_or("Context refresh absent")?;
                        current.refresh_pricing(
                            &id,
                            &prepared.expected,
                            &prepared.next,
                            &prepared.updates,
                            &prepared.manifests,
                        )
                    }
                    6 => {
                        let (expected, updates) = risk.ok_or("Risk requirement batch absent")?;
                        current.set_risk_requirements(&id, &expected, &updates)
                    }
                    _ => current.remove(&id, &token, deadline).await,
                };
                if result.is_ok() && matches!(action, 0 | 1 | 5 | 7) {
                    let latest = owner(&scopes).map_err(|error| format!("{error:?}"))?;
                    if latest
                        .as_ref()
                        .is_none_or(|owner| owner.generation() != expected)
                        || current.scope(&id)?.is_none()
                        || current
                            .status(&id)?
                            .is_none_or(|status| status.phase != Phase::Running)
                    {
                        return Err("Options scope readiness changed before completion".into());
                    }
                }
                result
            }
            .await;
            reservation.result = NativeControlResult::finished(result);
            drop(reservation);
            if deadline.remaining().is_zero() {
                Err(Outcome::DeadlineExpired)
            } else {
                Ok(0.0)
            }
        };
        if let Err(status) = instance.submit(operation, deadline, work) {
            instance
                .control
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .remove(&operation);
            return Err(status);
        }
        Ok(())
    })
}
/// Copies coherent native scope state. A missing/retiring scope succeeds with no
/// presence flags. This never acquires a subscription or returns a stale book.
/// # Safety
/// Scope is readable for scope_length bytes; output is aligned exclusive storage
/// for one 656-byte DTO, independent of the input.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_scope_state_v1(
    handle: u64,
    scope: *const u8,
    scope_length: u32,
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
        // SAFETY: Bounded readable scope name supplied by caller.
        let id = unsafe { name(scope, scope_length)? };
        let mut source = NativeMarketEvent {
            size: 128,
            lane: 3,
            instrument,
            ..Default::default()
        };
        source.fields[4] = publisher.into();
        let state = data(handle, id)?
            .map(|data| {
                data.publication_state(Lane::OptionsGlbx, instrument)
                    .map_err(|_| Status::InternalFailure)
            })
            .transpose()?
            .flatten();
        let publication = if let Some((generation, state)) = state {
            source.generation_low = generation as u64;
            source.generation_high = (generation >> 64) as u64;
            NativePublication::from_state(source, state)
        } else {
            NativePublication::from_state(source, Default::default())
        };
        // SAFETY: Validated aligned exclusive DTO destination.
        unsafe {
            output.write(publication);
        }
        Ok(())
    })
}
/// Drains at most sixteen ordered scoped publications into caller storage.
/// # Safety
/// Scope is readable for scope_length bytes, output has capacity aligned records,
/// and count is aligned exclusive independent storage. Null output only at zero.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_scope_publications_v1(
    handle: u64,
    scope: *const u8,
    scope_length: u32,
    output: *mut NativePublication,
    capacity: u32,
    count: *mut u32,
) -> i32 {
    boundary(|| {
        if !valid(count) || capacity > 16 || (capacity > 0 && !valid(output)) {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Bounded readable scope name supplied by caller.
        let id = unsafe { name(scope, scope_length)? };
        let mut batch: [_; 16] = std::array::from_fn(|_| empty_output());
        let copied = data(handle, id)?.map_or(Ok(0), |data| {
            data.drain_output(&mut batch[..capacity as usize])
                .map_err(|_| Status::InternalFailure)
        })?;
        for (index, value) in batch[..copied].iter().enumerate() {
            let publication = NativePublication::from_output(value);
            // SAFETY: Copy count fits the validated caller buffer.
            unsafe {
                output.add(index).write(publication);
            }
        }
        // SAFETY: Caller guarantees aligned independent writable count.
        unsafe {
            count.write(copied as u32);
        }
        Ok(())
    })
}
