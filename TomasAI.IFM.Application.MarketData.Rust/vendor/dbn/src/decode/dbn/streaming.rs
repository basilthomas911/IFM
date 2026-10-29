//! Incremental DBN v1..v3 metadata decoding with fixed input storage.
//!
//! The byte boundary is independent of the retained-state budget. No reservation
//! uses a wire count until checked against remaining bytes and that budget. A
//! cancelled parse is abandoned with its entire session, never resumed halfway.
use crate::{MappingInterval, Metadata, SType, Schema, SymbolMapping};
use std::{mem::size_of, num::NonZeroU64};
use tokio::io::{AsyncRead, AsyncReadExt, Take};

/// Fixed raw metadata scratch capacity, including fields crossing read boundaries.
pub const INPUT_CAPACITY: usize = 65_536;
/// Application retained-state policy, not a Databento protocol limit.
pub const DEFAULT_RETAINED_BUDGET: usize = 16 * 1024 * 1024;

/// Metadata fields needed by this consumer.
#[derive(Debug, Clone, Copy)]
pub enum Retention {
    /// Preserve every decoded field for history and differential qualification.
    All,
    /// Live readiness needs missing/partial symbols; requested symbols and historical
    /// mappings are validated but discarded. Live mappings arrive as records.
    Readiness,
}

/// Explicit resource policy. The complete u32 wire length remains streamable.
#[derive(Debug, Clone, Copy)]
pub struct Policy {
    /// Maximum total charged retained string bytes and vector element storage.
    pub retained_bytes: usize,
    /// Which variable fields to retain after validating them.
    pub retention: Retention,
}
impl Default for Policy {
    fn default() -> Self {
        Self {
            retained_bytes: DEFAULT_RETAINED_BUDGET,
            retention: Retention::All,
        }
    }
}

/// Actual byte/resource accounting for qualification and diagnostics.
#[derive(Debug, Clone, Copy)]
pub struct Metrics {
    /// Raw reusable buffer capacity; independent of metadata wire size.
    pub input_capacity: usize,
    /// Maximum number of bytes buffered at once.
    pub peak_buffered: usize,
    /// Bytes consumed, including the eight-byte prelude.
    pub consumed: u64,
    /// Charged retained payload and vector element bytes, excluding fixed structs.
    pub retained_bytes: usize,
}

fn invalid(message: &str) -> crate::Error {
    crate::Error::decode(message)
}

struct Body<R> {
    reader: Take<R>,
    buffer: Vec<u8>,
    pos: usize,
    end: usize,
    peak: usize,
    retained: usize,
    budget: usize,
}
impl<R: AsyncRead + Unpin> Body<R> {
    fn remaining(&self) -> u64 {
        self.reader.limit() + (self.end - self.pos) as u64
    }
    fn charge(&mut self, bytes: usize) -> crate::Result<()> {
        let total = self
            .retained
            .checked_add(bytes)
            .ok_or_else(|| invalid("metadata retained size overflow"))?;
        if total > self.budget {
            return Err(invalid("metadata retained-state budget exceeded"));
        }
        self.retained = total;
        Ok(())
    }
    async fn bytes(&mut self, n: usize) -> crate::Result<&[u8]> {
        if n > INPUT_CAPACITY || n as u64 > self.remaining() {
            return Err(invalid("metadata field exceeds remaining byte boundary"));
        }
        if self.end - self.pos < n {
            self.buffer.copy_within(self.pos..self.end, 0);
            self.end -= self.pos;
            self.pos = 0;
            while self.end < n {
                // Cooperative even for an always-ready synthetic/socket source.
                tokio::task::yield_now().await;
                let read = self
                    .reader
                    .read(&mut self.buffer[self.end..])
                    .await
                    .map_err(|e| crate::Error::io(e, "streaming DBN metadata"))?;
                if read == 0 {
                    return Err(invalid("truncated DBN metadata"));
                }
                self.end += read;
                self.peak = self.peak.max(self.end);
            }
        }
        let start = self.pos;
        self.pos += n;
        Ok(&self.buffer[start..start + n])
    }
    async fn u8(&mut self) -> crate::Result<u8> {
        Ok(self.bytes(1).await?[0])
    }
    async fn u16(&mut self) -> crate::Result<u16> {
        Ok(u16::from_le_bytes(self.bytes(2).await?.try_into().unwrap()))
    }
    async fn u32(&mut self) -> crate::Result<u32> {
        Ok(u32::from_le_bytes(self.bytes(4).await?.try_into().unwrap()))
    }
    async fn u64(&mut self) -> crate::Result<u64> {
        Ok(u64::from_le_bytes(self.bytes(8).await?.try_into().unwrap()))
    }
    async fn symbol(&mut self, width: usize, keep: bool) -> crate::Result<String> {
        let text = std::str::from_utf8(self.bytes(width).await?)
            .map_err(|_| invalid("invalid UTF-8 in metadata symbol"))?
            .trim_end_matches('\0');
        if !keep {
            return Ok(String::new());
        }
        let len = text.len();
        let mut out = String::new();
        // The field borrow is released before charging/reserving.
        self.charge(len)?;
        out.try_reserve_exact(len)
            .map_err(|_| invalid("metadata string allocation failed"))?;
        let text = std::str::from_utf8(&self.buffer[self.pos - width..self.pos])
            .unwrap()
            .trim_end_matches('\0');
        out.push_str(text);
        Ok(out)
    }
    async fn count(&mut self, minimum: usize) -> crate::Result<usize> {
        let count = self.u32().await? as usize;
        let needed = (count as u64)
            .checked_mul(minimum as u64)
            .ok_or_else(|| invalid("metadata count overflow"))?;
        if needed > self.remaining() {
            return Err(invalid("metadata count exceeds remaining encoded bytes"));
        }
        Ok(count)
    }
    fn vector<T>(&mut self, count: usize, keep: bool) -> crate::Result<Vec<T>> {
        let mut out = Vec::new();
        if keep {
            self.charge(
                count
                    .checked_mul(size_of::<T>())
                    .ok_or_else(|| invalid("metadata vector size overflow"))?,
            )?;
            out.try_reserve_exact(count)
                .map_err(|_| invalid("metadata vector allocation failed"))?;
        }
        Ok(out)
    }
    async fn symbols(&mut self, width: usize, keep: bool) -> crate::Result<Vec<String>> {
        let count = self.count(width).await?;
        let mut out = self.vector(count, keep)?;
        for i in 0..count {
            let symbol = self.symbol(width, keep).await?;
            if keep {
                out.push(symbol);
            }
            if i % 256 == 0 {
                tokio::task::yield_now().await;
            }
        }
        Ok(out)
    }
}

fn date(raw: u32) -> crate::Result<time::Date> {
    let month = time::Month::try_from(((raw % 10_000) / 100) as u8)
        .map_err(|_| invalid("invalid mapping month"))?;
    time::Date::from_calendar_date((raw / 10_000) as i32, month, (raw % 100) as u8)
        .map_err(|_| invalid("invalid mapping date"))
}

/// Decodes exactly one metadata block. Caller supplies cancellation/deadline around
/// this future and must discard the reader after interruption or failure. No bytes
/// past the declared block are consumed. Invalid and unbudgeted data fail explicitly.
pub async fn decode<R: AsyncRead + Unpin>(
    reader: &mut R,
    policy: Policy,
) -> crate::Result<(Metadata, Metrics)> {
    let mut prelude = [0; 8];
    reader
        .read_exact(&mut prelude)
        .await
        .map_err(|e| crate::Error::io(e, "DBN metadata prelude"))?;
    if &prelude[..3] != b"DBN" || !(1..=3).contains(&prelude[3]) {
        return Err(invalid("invalid DBN prefix/version"));
    }
    let version = prelude[3];
    let length = u32::from_le_bytes(prelude[4..].try_into().unwrap());
    if length < 100 {
        return Err(invalid("metadata shorter than fixed header"));
    }
    let mut buffer = Vec::new();
    buffer
        .try_reserve_exact(INPUT_CAPACITY)
        .map_err(|_| invalid("metadata input buffer allocation failed"))?;
    buffer.resize(INPUT_CAPACITY, 0);
    let mut body = Body {
        reader: reader.take(u64::from(length)),
        buffer,
        pos: 0,
        end: 0,
        peak: 0,
        retained: 0,
        budget: policy.retained_bytes,
    };
    let dataset = body.symbol(16, true).await?;
    let raw_schema = body.u16().await?;
    let schema = if raw_schema == u16::MAX {
        None
    } else {
        Some(Schema::try_from(raw_schema).map_err(|_| invalid("invalid metadata schema"))?)
    };
    let start = body.u64().await?;
    let raw_end = body.u64().await?;
    let limit = NonZeroU64::new(body.u64().await?);
    if version == 1 {
        body.u64().await?;
    }
    let raw_stype = body.u8().await?;
    let stype_in = if raw_stype == u8::MAX {
        None
    } else {
        Some(SType::try_from(raw_stype).map_err(|_| invalid("invalid metadata input symbology"))?)
    };
    let stype_out = SType::try_from(body.u8().await?)
        .map_err(|_| invalid("invalid metadata output symbology"))?;
    let ts_out = body.u8().await? != 0;
    let width = if version == 1 {
        22
    } else {
        usize::from(body.u16().await?)
    };
    if width == 0 {
        return Err(invalid("zero metadata symbol width"));
    }
    body.bytes(if version == 1 { 47 } else { 53 }).await?;
    if body.u32().await? != 0 {
        return Err(invalid("unsupported metadata schema definition"));
    }
    let all = matches!(policy.retention, Retention::All);
    let symbols = body.symbols(width, all).await?;
    let partial = body.symbols(width, true).await?;
    let not_found = body.symbols(width, true).await?;
    let count = body.count(width + 4).await?;
    let mut mappings = body.vector(count, all)?;
    for i in 0..count {
        let raw_symbol = body.symbol(width, all).await?;
        let count = body.count(width + 8).await?;
        let mut intervals = body.vector(count, all)?;
        for j in 0..count {
            let start_date = date(body.u32().await?)?;
            let end_date = date(body.u32().await?)?;
            let symbol = body.symbol(width, all).await?;
            if all {
                intervals.push(MappingInterval {
                    start_date,
                    end_date,
                    symbol,
                });
            }
            if j % 256 == 0 {
                tokio::task::yield_now().await;
            }
        }
        if all {
            mappings.push(SymbolMapping {
                raw_symbol,
                intervals,
            });
        }
        if i % 256 == 0 {
            tokio::task::yield_now().await;
        }
    }
    // Upstream accepts trailing metadata bytes. Validate/read through them rather
    // than interpreting them as records; retain none (includes v3 alignment).
    while body.remaining() != 0 {
        let n = body.remaining().min(INPUT_CAPACITY as u64) as usize;
        body.bytes(n).await?;
    }
    let metrics = Metrics {
        input_capacity: body.buffer.capacity(),
        peak_buffered: body.peak,
        consumed: u64::from(length) + 8,
        retained_bytes: body.retained,
    };
    Ok((
        Metadata {
            version,
            dataset,
            schema,
            start,
            end: if raw_end == crate::UNDEF_TIMESTAMP {
                None
            } else {
                NonZeroU64::new(raw_end)
            },
            limit,
            stype_in,
            stype_out,
            ts_out,
            symbol_cstr_len: width,
            symbols,
            partial,
            not_found,
            mappings,
        },
        metrics,
    ))
}
