//! Shared FMP retry/circuit policy. Admission and every retry consume the same
//! original operation deadline. Circuit time uses the explicit API clock.
use super::*;
use crate::historical::Failure;
use rand::Rng;
impl Fmp {
    fn circuit_open(&self) -> Result<()> {
        let now = self.now()?;
        let mut state = self
            .circuit
            .lock()
            .map_err(|_| Error::new("Internal", "FMP circuit owner interrupted"))?;
        if let Some(until) = state.until {
            if now < until {
                return Err(Error::new("Unavailable", "FMP circuit is open"));
            }
            state.until = None;
            state.failures = 0;
        }
        Ok(())
    }
    fn success(&self) -> Result<()> {
        *self
            .circuit
            .lock()
            .map_err(|_| Error::new("Internal", "FMP circuit owner interrupted"))? =
            Circuit::default();
        Ok(())
    }
    fn transient_failure(&self) -> Result<()> {
        let now = self.now()?;
        let mut state = self
            .circuit
            .lock()
            .map_err(|_| Error::new("Internal", "FMP circuit owner interrupted"))?;
        state.failures = state.failures.saturating_add(1);
        if state.failures >= self.options.circuit_failures {
            let ticks = i64::try_from(self.options.circuit_break.as_nanos() / 100)
                .map_err(|_| Error::new("Configuration", "FMP circuit duration overflow"))?;
            state.until = Some(
                now.checked_add(ticks)
                    .ok_or_else(|| Error::new("Clock", "FMP circuit timestamp overflow"))?,
            );
        }
        Ok(())
    }
    fn delay(&self, header: Option<&str>, attempt: u32) -> Duration {
        let delay=header.and_then(|s|s.parse::<u64>().ok().map(Duration::from_secs).or_else(||{
            // RFC 9110's HTTP-date uses fixed GMT, not the machine's time zone.
            let format=time::format_description::parse_borrowed::<2>("[weekday repr:short], [day] [month repr:short] [year] [hour]:[minute]:[second] GMT").ok()?;
            let date=PrimitiveDateTime::parse(s,&format).ok()?.assume_utc();let now=self.now().ok()?;
            let ticks=date.unix_timestamp_nanos()/100-i128::from(now);
            Some(Duration::from_nanos(u64::try_from((ticks.max(0)*100).min(i128::from(u64::MAX))).ok()?))
        }));
        delay
            .unwrap_or_else(|| {
                let base = self.options.initial_delay.as_secs_f64() * 2f64.powi(attempt as i32);
                Duration::from_secs_f64(
                    (base * (1.0 + rand::rng().random_range(0.0..0.25)))
                        .min(self.options.maximum_delay.as_secs_f64()),
                )
            })
            .min(self.options.maximum_delay)
    }
    pub(super) async fn get(
        &self,
        path: &str,
        from: Date,
        to: Date,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<u8>> {
        Self::check(token, deadline)?;
        self.circuit_open()?;
        let mut target = reqwest::Url::parse(&self.options.base)
            .and_then(|b| b.join(path))
            .map_err(|_| Error::new("Configuration", "Invalid FMP endpoint"))?;
        target
            .query_pairs_mut()
            .append_pair("from", &from.to_string())
            .append_pair("to", &to.to_string());
        for attempt in 0..=self.options.retries {
            let outcome = {
                let _permit = deadline
                    .run(token, self.requests.acquire())
                    .await
                    .map_err(Error::interrupted)?
                    .map_err(|_| Error::new("Internal", "FMP admission owner closed"))?;
                self.http
                    .reference_json(target.clone(), self.options.response_bytes, token, deadline)
                    .await
            };
            let delay = match outcome {
                Ok((status, _, body)) if (200..300).contains(&status) => {
                    self.success()?;
                    return Ok(body);
                }
                Ok((status, header, _)) => {
                    let transient = matches!(status, 408 | 429 | 500 | 502 | 503 | 504);
                    if transient && attempt < self.options.retries {
                        self.delay(header.as_deref(), attempt)
                    } else {
                        if transient {
                            self.transient_failure()?;
                        }
                        let mut error = Error::new(
                            match status {
                                401 | 403 => "Authentication",
                                429 => "RateLimit",
                                _ if transient => "Unavailable",
                                _ => "Response",
                            },
                            format!("FMP returned HTTP status {status}"),
                        );
                        error.status = Some(status);
                        return Err(error);
                    }
                }
                Err(Failure::Interrupted(reason)) => return Err(Error::interrupted(reason)),
                Err(Failure::Input(message)) => return Err(Error::new("Configuration", message)),
                Err(Failure::Processing(message)) => return Err(Error::new("Processing", message)),
                Err(Failure::Provider(message))
                    if message.contains("exceeds bounded body policy") =>
                {
                    return Err(Error::new("ResponseTooLarge", message));
                }
                Err(error) => {
                    if attempt == self.options.retries {
                        self.transient_failure()?;
                        return Err(Error::new(
                            "Unavailable",
                            format!("FMP unavailable after bounded retries: {error:?}"),
                        ));
                    }
                    self.delay(None, attempt)
                }
            };
            deadline
                .run(token, tokio::time::sleep(delay))
                .await
                .map_err(Error::interrupted)?;
        }
        Err(Error::new("Internal", "FMP retry state exhausted"))
    }
}
