//! Copied native scope-claim command tokens. Tokens carry no resource ownership;
//! retaining one in C# cannot delay dataset cancellation, expiry or replacement.
use super::{
    runtime::{Status, boundary, instance, valid},
    scopes,
};
use crate::{
    runtime::RECOVERY_BUDGET,
    subscriptions::{Token, leases::Lifetime},
};
use std::time::Duration;

/// Fixed 64-byte command identity, never a pointer or reference to native resources.
#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq, Eq)]
pub struct NativeScopeClaim {
    /// ABI size, exactly 64.
    pub size: u32,
    /// Reserved, zero.
    pub flags: u32,
    /// Dataset generation in CLR Guid little-endian ABI words.
    pub generation: [u64; 2],
    /// Monotonic claim identity within this generation.
    pub serial: u64,
    /// Stable business intent identity, not an application task/lease handle.
    pub owner: [u64; 2],
    /// Reserved, zero.
    pub reserved: [u64; 2],
}
impl NativeScopeClaim {
    fn token(&self) -> Result<Token, Status> {
        let generation = u128::from(self.generation[0]) | (u128::from(self.generation[1]) << 64);
        let owner = u128::from(self.owner[0]) | (u128::from(self.owner[1]) << 64);
        if self.size != 64
            || self.flags != 0
            || self.reserved != [0; 2]
            || generation == 0
            || owner == 0
            || self.serial == 0
        {
            return Err(Status::InvalidInput);
        }
        Ok(Token {
            generation,
            owner,
            serial: self.serial,
        })
    }
    fn from_token(token: Token) -> Self {
        Self {
            size: 64,
            generation: [token.generation as u64, (token.generation >> 64) as u64],
            serial: token.serial,
            owner: [token.owner as u64, (token.owner >> 64) as u64],
            ..Self::default()
        }
    }
}
/// Atomically imports complete scope intent without provider/control admission.
/// JSON is an array of at most 64 names. A zero TTL means durable intent; positive
/// TTL expires natively within at most 120 seconds. No caller timer is retained.
///
/// # Safety
/// Input is readable for length (1..32768) bytes. Output is independent, aligned,
/// exclusively writable 64-byte storage and remains unchanged on failure.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_scope_claim_v1(
    handle: u64,
    generation_low: u64,
    generation_high: u64,
    owner_low: u64,
    owner_high: u64,
    input: *const u8,
    length: u32,
    ttl_ms: u64,
    size: u32,
    output: *mut NativeScopeClaim,
) -> i32 {
    boundary(|| {
        if input.is_null() || length == 0 || length > 32768 || !valid(output) {
            return Err(Status::InvalidInput);
        }
        if size != 64 {
            return Err(Status::AbiMismatch);
        }
        if ttl_ms > RECOVERY_BUDGET.as_millis() as u64 {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Bounded caller-owned bytes, copied before the native mutation.
        let names: Vec<String> =
            serde_json::from_slice(unsafe { std::slice::from_raw_parts(input, length as usize) })
                .map_err(|_| Status::InvalidInput)?;
        if names.is_empty()
            || names.len() > 64
            || names
                .iter()
                .any(|s| s.trim().is_empty() || s.len() > 512 || s.encode_utf16().count() > 128)
        {
            return Err(Status::InvalidInput);
        }
        let generation = u128::from(generation_low) | (u128::from(generation_high) << 64);
        let owner = u128::from(owner_low) | (u128::from(owner_high) << 64);
        let instance = instance(handle)?;
        let current = scopes::owner(&instance.scopes)?.ok_or(Status::InvalidInput)?;
        if generation == 0 || owner == 0 || generation != current.generation() {
            return Err(Status::InvalidInput);
        }
        let lifetime = if ttl_ms == 0 {
            Lifetime::Durable
        } else {
            Lifetime::Ephemeral(Duration::from_millis(ttl_ms))
        };
        let token = current
            .retain_scopes(owner, &names, lifetime)
            .map_err(|_| Status::InvalidInput)?;
        // SAFETY: Validated independent aligned exclusive destination.
        unsafe {
            output.write(NativeScopeClaim::from_token(token));
        }
        Ok(())
    })
}
/// Renews an exact active ephemeral token with no provider or command-gate wait.
/// Expired or retired tokens cannot reopen admission or resurrect subscriptions.
///
/// # Safety
/// Claim points to one aligned readable 64-byte copied record for this call only.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_scope_claim_renew_v1(
    handle: u64,
    claim: *const NativeScopeClaim,
    ttl_ms: u64,
) -> i32 {
    boundary(|| {
        if !valid(claim.cast_mut()) || ttl_ms == 0 || ttl_ms > 120000 {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Validated aligned caller-owned readable record, never retained.
        let token = unsafe { claim.read() }.token()?;
        let instance = instance(handle)?;
        let current = scopes::owner(&instance.scopes)?.ok_or(Status::InvalidInput)?;
        current
            .renew_claim(token, Duration::from_millis(ttl_ms))
            .map_err(|_| Status::InvalidInput)
    })
}
/// Releases copied intent and signals native expiry/cleanup without waiting for
/// provider I/O. Success is intent admission, not physical cleanup proof. A stale
/// generation is an idempotent no-op and cannot remove replacement subscriptions.
///
/// # Safety
/// Claim points to one aligned readable 64-byte copied record for this call only.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_scope_claim_release_v1(
    handle: u64,
    claim: *const NativeScopeClaim,
) -> i32 {
    boundary(|| {
        if !valid(claim.cast_mut()) {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Validated aligned caller-owned readable record, never retained.
        let token = unsafe { claim.read() }.token()?;
        let instance = instance(handle)?;
        if let Some(current) = scopes::owner(&instance.scopes)?
            && current.generation() == token.generation
        {
            current
                .release_claim(token)
                .map_err(|_| Status::InternalFailure)?;
        }
        Ok(())
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn token_layout_and_roundtrip_do_not_contain_resource_handles() {
        assert_eq!(std::mem::size_of::<NativeScopeClaim>(), 64);
        assert_eq!(std::mem::offset_of!(NativeScopeClaim, serial), 24);
        assert_eq!(std::mem::offset_of!(NativeScopeClaim, owner), 32);
        let token = Token {
            generation: u128::MAX,
            serial: u64::MAX,
            owner: 123,
        };
        assert_eq!(NativeScopeClaim::from_token(token).token().unwrap(), token);
        let mut copy = NativeScopeClaim::from_token(token);
        copy.reserved[1] = 1;
        assert!(copy.token().is_err());
        copy = NativeScopeClaim::from_token(token);
        copy.serial = 0;
        assert!(copy.token().is_err());
    }
}
