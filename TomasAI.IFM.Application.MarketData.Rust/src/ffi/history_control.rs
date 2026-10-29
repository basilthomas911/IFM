//! Historical estimate and paid-job controls. Submission is never retried here:
//! application persistence owns the provider job identity and charging decision.
use super::{
    retained::{CAP, Pending, encode},
    runtime::{Status, boundary, instance},
};
use crate::{
    historical::Failure,
    runtime::{Deadline, owner::Outcome},
};
use databento::{
    dbn::{Compression, Encoding, SType, Schema},
    historical::{
        batch::{BatchJob, Delivery, JobState, SplitDuration, SubmitJobParams},
        metadata::GetQueryParams,
    },
};
use serde_json::{Value, json};
use std::{num::NonZeroU64, time::Duration};
use tokio_util::sync::CancellationToken;

pub(super) fn schema(value: u64) -> Result<Schema, Status> {
    match value {
        1 => Ok(Schema::Definition),
        2 => Ok(Schema::Ohlcv1M),
        3 => Ok(Schema::Trades),
        4 => Ok(Schema::Statistics),
        5 => Ok(Schema::Ohlcv1D),
        _ => Err(Status::InvalidInput),
    }
}
fn schema_id(value: Schema) -> u32 {
    match value {
        Schema::Definition => 1,
        Schema::Ohlcv1M => 2,
        Schema::Trades => 3,
        Schema::Statistics => 4,
        Schema::Ohlcv1D => 5,
        _ => 0,
    }
}
pub(super) fn request(v: &Value) -> Result<GetQueryParams, Status> {
    let dataset = v["dataset"]
        .as_str()
        .filter(|s| !s.is_empty() && s.len() <= 64 && s.is_ascii())
        .ok_or(Status::InvalidInput)?;
    let symbols = v["symbols"]
        .as_array()
        .filter(|s| !s.is_empty() && s.len() <= 2048)
        .ok_or(Status::InvalidInput)?;
    let symbols = symbols
        .iter()
        .map(|s| {
            s.as_str()
                .filter(|s| !s.is_empty() && s.len() <= 70 && s.is_ascii() && !s.contains(','))
                .map(str::to_owned)
                .ok_or(Status::InvalidInput)
        })
        .collect::<Result<Vec<_>, _>>()?;
    let start = time::OffsetDateTime::from_unix_timestamp_nanos(i128::from(
        v["start"].as_i64().ok_or(Status::InvalidInput)?,
    ))
    .map_err(|_| Status::InvalidInput)?;
    let end = time::OffsetDateTime::from_unix_timestamp_nanos(i128::from(
        v["end"].as_i64().ok_or(Status::InvalidInput)?,
    ))
    .map_err(|_| Status::InvalidInput)?;
    if start >= end {
        return Err(Status::InvalidInput);
    }
    let stype = match v["symbology"].as_u64() {
        Some(1) => SType::RawSymbol,
        Some(2) => SType::Continuous,
        Some(3) => SType::InstrumentId,
        _ => return Err(Status::InvalidInput),
    };
    let limit = v["limit"].as_u64().ok_or(Status::InvalidInput)?;
    Ok(GetQueryParams::builder()
        .dataset(dataset)
        .symbols(symbols)
        .schema(schema(v["schema"].as_u64().ok_or(Status::InvalidInput)?)?)
        .stype_in(stype)
        .date_time_range(start..end)
        .maybe_limit(NonZeroU64::new(limit))
        .build())
}
fn job(job: BatchJob) -> Value {
    json!({"providerJobId":job.id,"state":match job.state{JobState::Queued=>"Queued",JobState::Processing=>"Processing",JobState::Done=>"Completed",JobState::Expired=>"Expired"},"costUsd":job.cost_usd.unwrap_or(0.0).to_string(),"recordCount":job.record_count.unwrap_or(0),"billedBytes":job.billed_size.unwrap_or(0),"progressPercent":job.progress.unwrap_or(u8::from(job.state==JobState::Done)*100)})
}
/// Submits mode 0 estimate, 1 paid batch creation, 2 existing job status or 3
/// immutable batch file listing; mode 4 reads schema availability. JSON is copied before returning; query results
/// use the standard retained reference take/discard ownership contract. One
/// original caller budget covers every provider call (at most thirty minutes).
/// # Safety
/// Input is readable for length bytes (1..256 KiB), borrowed only for this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_history_control_v1(
    handle: u64,
    operation: u64,
    mode: u32,
    input: *const u8,
    length: u32,
    budget_ms: u64,
) -> i32 {
    let deadline = Deadline::operation(Duration::from_millis(budget_ms));
    boundary(|| {
        if mode > 4 || input.is_null() || length == 0 || length > 262_144 {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Caller provides readable bounded query storage; all values are owned.
        let value: Value =
            serde_json::from_slice(unsafe { std::slice::from_raw_parts(input, length as usize) })
                .map_err(|_| Status::InvalidInput)?;
        let query = if mode <= 1 || mode == 4 {
            Some(request(&value)?)
        } else {
            None
        };
        let id = if matches!(mode, 2 | 3) {
            value["job"]
                .as_str()
                .filter(|s| !s.is_empty() && s.len() <= 256 && !s.chars().any(char::is_control))
                .ok_or(Status::InvalidInput)?
                .to_owned()
        } else {
            String::new()
        };
        let instance = instance(handle)?;
        let history = instance
            .history
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .clone()
            .ok_or(Status::InvalidInput)?;
        let clock = instance.reference_clock()?;
        let mut pending = Pending::reserve(&instance, operation)?;
        let mut reservation = instance.reserve_control(operation)?;
        let work = async move {
            let token = CancellationToken::new();
            let result=async {
                let output=match mode {
                    4=>{let query=query.expect("mode checked");
                        let end=history.available_end(&query.dataset,query.schema,&token,deadline).await?;
                        json!({"end":i64::try_from(end.unix_timestamp_nanos()).map_err(|_|Failure::Input("Historical availability timestamp overflow"))?})},
                    0=>{let query=query.expect("mode checked");let cost=history.cost(query.clone(),&token,deadline).await?;let bytes=history.billable_size(query.clone(),&token,deadline).await?;let records=history.record_count(query,&token,deadline).await?;
                        json!({"cost":cost,"bytes":bytes,"records":records,"observed":clock.utc_ticks().map_err(|_|Failure::Input("Invalid API clock"))?})},
                    1=>{let q=query.expect("mode checked");let params=SubmitJobParams::builder().dataset(q.dataset).symbols(q.symbols).schema(q.schema).date_time_range(q.date_time_range).encoding(Encoding::Dbn).compression(Compression::Zstd).split_duration(SplitDuration::Month).delivery(Delivery::Download).stype_in(q.stype_in).stype_out(SType::InstrumentId).maybe_limit(q.limit).build();job(history.submit_batch(&params,&token,deadline).await?)},
                    2=>job(history.batch_details(&id,&token,deadline).await?),
                    3=>{let job=history.batch_details(&id,&token,deadline).await?;let schema=schema_id(job.schema);let files=history.batch_files(&id,&token,deadline).await?;
                        json!({"files":files.into_iter().filter(|f|f.filename.ends_with(".dbn")||f.filename.ends_with(".dbn.zst")).map(|f|json!({"providerFileId":f.filename,"fileName":f.filename,"schema":schema,"sizeBytes":f.size,"sha256":f.hash.strip_prefix("sha256:").unwrap_or(&f.hash)})).collect::<Vec<_>>()})},
                    _=>unreachable!("mode checked"),
                };
                pending.commit(encode(&output).map_err(Failure::Processing)?,CAP).map_err(Failure::Processing)
            }.await;
            reservation.result = super::live::NativeControlResult::finished(
                result.map_err(|e| format!("Historical control failed: {e:?}")),
            );
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
