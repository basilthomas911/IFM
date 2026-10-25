use crate::backend::{contracts::*, transport::Backend};
use chrono::{Datelike, NaiveDate};
use rmpv::Value;

#[derive(Clone, Debug)]
pub struct BusinessContext {
    pub value_date: NaiveDate,
    pub active_value_date: Option<NaiveDate>,
    pub as_of_utc: chrono::DateTime<chrono::Utc>,
    pub revision: i64,
    pub market_state: i64,
    pub allow_development_emulator: bool,
}
impl BusinessContext {
    pub async fn load(backend: &Backend) -> Result<Self, String> {
        let subject = ActorSubject::query("MarketDataQuery", "GetMarketSession", "marketSession");
        let reply = backend
            .request(
                &subject,
                &Value::Array(vec![subject.wire(), Value::Nil]),
                None,
                None,
            )
            .await?;
        let session = service_result(&reply)?
            .as_array()
            .ok_or("Invalid market session reply")?;
        if session.len() < 12 {
            return Err("Incomplete market session".into());
        }
        let context = Self {
            value_date: date_only(&session[0])?,
            active_value_date: if session[1].is_nil() {
                None
            } else {
                Some(date_only(&session[1])?)
            },
            as_of_utc: timestamp(&session[8])?,
            revision: session[7].as_i64().ok_or("Invalid session revision")?,
            market_state: session[9].as_i64().ok_or("Invalid market state")?,
            allow_development_emulator: session[11]
                .as_bool()
                .ok_or("Invalid API emulator permission")?,
        };
        if context.revision <= 0
            || !(0..=2).contains(&context.market_state)
            || context.active_value_date.is_some() != (context.market_state != 0)
        {
            return Err("Invalid API market session state".into());
        }
        let start = timestamp(&session[4])?;
        let end = timestamp(&session[5])?;
        if start.year() <= 1
            || end <= start
            || timestamp(&session[6])?.year() <= 1
            || context.as_of_utc.year() <= 1
        {
            return Err("Invalid API market-session timing".into());
        }
        Ok(context)
    }
    /// API startup can precede actor readiness. Retry read-only hydration within
    /// one absolute budget, never start feeds or synthesize a business date.
    pub async fn await_ready(
        backend: &Backend,
        deadline: tokio::time::Instant,
    ) -> Result<Self, String> {
        let mut last = "API session is not ready".to_string();
        loop {
            let result = tokio::time::timeout_at(deadline, async {
                let context = Self::load(backend).await?;
                let subject = ActorSubject::query("MarketDataQuery", "GetValueDate", "valueDate");
                let reply = backend
                    .request_until(
                        &subject,
                        &Value::Array(vec![subject.wire(), Value::Nil]),
                        deadline,
                        None,
                    )
                    .await?;
                let value = service_result(&reply)?
                    .as_array()
                    .and_then(|a| a.first())
                    .ok_or("Missing API ValueDate")?;
                if date_only(value)? != context.value_date {
                    return Err("API ValueDate changed during initialization".to_string());
                }
                Ok::<_, String>(context)
            })
            .await;
            match result {
                Ok(Ok(context)) => return Ok(context),
                Ok(Err(error)) => last = error,
                Err(_) => return Err(format!("API initialization deadline expired: {last}")),
            }
            if tokio::time::Instant::now() >= deadline {
                return Err(format!("API initialization deadline expired: {last}"));
            }
            tokio::time::sleep_until(
                deadline.min(tokio::time::Instant::now() + std::time::Duration::from_millis(500)),
            )
            .await;
        }
    }
}
