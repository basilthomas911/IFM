//! Cold FMP configuration and bounded result transfer. Credentials cross only
//! the configure call; operations retain no foreign pointer or detached task.
use super::retained::{CAP, Pending, encode};
use super::runtime::{Status, boundary, instance};
use crate::{
    dns::Resolver,
    reference::fmp::{Fmp, Options},
    runtime::owner::{Outcome, foreign_deadline},
};
use std::{sync::Arc, time::Duration};
use tokio_util::sync::CancellationToken;
/// Configures one native FMP owner after the explicit API clock. JSON uses the
/// existing option names in camelCase plus `key` and numeric `dns` addresses.
/// No network I/O occurs. Reconfiguration returns Busy.
/// # Safety
/// Input is readable for length bytes (1..16,384), borrowed only during this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_fmp_configure_v1(
    handle: u64,
    input: *const u8,
    length: u32,
) -> i32 {
    boundary(|| {
        if input.is_null() || length == 0 || length > 16_384 {
            return Err(Status::InvalidInput);
        }
        // SAFETY: The caller supplies readable bytes; all retained fields are copied.
        let v: serde_json::Value =
            serde_json::from_slice(unsafe { std::slice::from_raw_parts(input, length as usize) })
                .map_err(|_| Status::InvalidInput)?;
        let number = |key: &str| v[key].as_u64().ok_or(Status::InvalidInput);
        let text = |key: &str| {
            v[key]
                .as_str()
                .map(str::to_owned)
                .ok_or(Status::InvalidInput)
        };
        let size = |key: &str| usize::try_from(number(key)?).map_err(|_| Status::InvalidInput);
        let small = |key: &str| u32::try_from(number(key)?).map_err(|_| Status::InvalidInput);
        let options = Options {
            base: text("baseAddress")?,
            treasury: text("treasuryRatesEndpoint")?,
            calendar: text("economicCalendarEndpoint")?,
            window_days: small("maximumProviderWindowDays")?,
            range_days: small("maximumRequestRangeDays")?,
            lookback_days: small("latestTreasuryLookbackDays")?,
            response_bytes: size("maximumResponseBytes")?,
            rows: size("maximumNormalizedRows")?,
            concurrency: size("maximumConcurrentRequests")?,
            retries: small("maximumRetryAttempts")?,
            initial_delay: Duration::from_millis(number("initialRetryDelayMs")?),
            maximum_delay: Duration::from_millis(number("maximumRetryDelayMs")?),
            circuit_failures: small("circuitBreakerFailureThreshold")?,
            circuit_break: Duration::from_millis(number("circuitBreakerBreakDurationMs")?),
            operation_timeout: Duration::from_millis(number("totalOperationTimeoutMs")?),
            request_timeout: Duration::from_millis(number("requestTimeoutMs")?),
        };
        let dns = v["dns"].as_array().ok_or(Status::InvalidInput)?;
        if dns.is_empty() || dns.len() > 4 {
            return Err(Status::InvalidInput);
        }
        let servers = dns
            .iter()
            .map(|s| {
                s.as_str()
                    .ok_or(Status::InvalidInput)?
                    .parse()
                    .map_err(|_| Status::InvalidInput)
            })
            .collect::<Result<Vec<_>, _>>()?;
        let instance = instance(handle)?;
        let clock = instance.reference_clock()?;
        let resolver = Arc::new(
            Resolver::with_slots(servers, instance.dns_slots.clone())
                .map_err(|_| Status::InvalidInput)?,
        );
        let source = Fmp::new(options, text("key")?, resolver, clock)
            .map_err(|_| Status::InvalidInput)?
            .with_notifications(instance.owner.clone());
        let mut slot = instance.fmp.lock().map_err(|_| Status::InternalFailure)?;
        if slot.is_some() {
            return Err(Status::Busy);
        }
        *slot = Some(Arc::new(source));
        Ok(())
    })
}
/// Submits mode 0 Treasury range, 1 latest Treasury, or 2 calendar. Query JSON
/// contains explicit yyyyMMdd `from`, `to` and an optional `countries` string array.
/// Results or typed failures are copied with reference_take and then consumed.
/// # Safety
/// Input is readable for length bytes (1..8192); no pointer is retained.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_fmp_submit_v1(
    handle: u64,
    operation: u64,
    mode: u32,
    input: *const u8,
    length: u32,
    budget_ms: u64,
) -> i32 {
    let deadline = foreign_deadline(budget_ms);
    boundary(|| {
        if mode > 2 || input.is_null() || length == 0 || length > 8192 {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Bounded caller-readable query bytes, copied before submission.
        let v: serde_json::Value =
            serde_json::from_slice(unsafe { std::slice::from_raw_parts(input, length as usize) })
                .map_err(|_| Status::InvalidInput)?;
        let to = super::reference::date(
            v["to"]
                .as_u64()
                .and_then(|n| u32::try_from(n).ok())
                .ok_or(Status::InvalidInput)?,
        )?;
        let from = if mode == 1 {
            to
        } else {
            super::reference::date(
                v["from"]
                    .as_u64()
                    .and_then(|n| u32::try_from(n).ok())
                    .ok_or(Status::InvalidInput)?,
            )?
        };
        let countries = if let Some(countries) = v.get("countries") {
            let countries = countries.as_array().ok_or(Status::InvalidInput)?;
            if countries.len() > 256 {
                return Err(Status::InvalidInput);
            }
            countries
                .iter()
                .map(|c| c.as_str().map(str::to_owned).ok_or(Status::InvalidInput))
                .collect::<Result<Vec<_>, _>>()?
        } else {
            Vec::new()
        };
        let instance = instance(handle)?;
        let source = instance
            .fmp
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .clone()
            .ok_or(Status::InvalidInput)?;
        let mut pending = Pending::reserve(&instance, operation)?;
        let mut reservation = instance.reserve_control(operation)?;
        let work = async move {
            let token = CancellationToken::new();
            let result=async {
                let rows=if mode==2 {
                    source.calendar(from,to,&countries,&token,deadline).await.map(|rows|serde_json::Value::Array(rows.into_iter().map(|e|serde_json::json!({"at":e.at,"country":e.country,"name":e.name,"values":e.values,"retrieved":e.retrieved,"source":"FinancialModelingPrep"})).collect()))
                }else{
                    let rows=if mode==0{source.treasury_range(from,to,&token,deadline).await}else{source.treasury_latest(to,&token,deadline).await.map(|row|row.into_iter().collect())};
                    rows.map(|rows|serde_json::Value::Array(rows.into_iter().map(|row|serde_json::json!({"date":row.date.year()*10000+row.date.month()as i32*100+i32::from(row.date.day()),"retrieved":row.retrieved,"source":"FinancialModelingPrep","rates":row.rates.iter().map(|point|serde_json::json!({"months":point.months,"bits":point.percentage.bits})).collect::<Vec<_>>()})).collect()))
                };
                let value=rows.unwrap_or_else(|e|serde_json::json!({"failure":{"code":e.code,"message":e.message,"status":e.status}}));
                pending.commit(encode(&value)?,CAP)?;Ok(())
            }.await;
            reservation.result = super::live::NativeControlResult::finished(result);
            drop(pending);
            drop(reservation);
            if deadline.remaining().is_zero() {
                Err(Outcome::DeadlineExpired)
            } else {
                Ok(0.0)
            }
        };
        if let Err(error) = instance.submit(operation, deadline, work) {
            instance
                .control
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .remove(&operation);
            return Err(error);
        }
        Ok(())
    })
}
