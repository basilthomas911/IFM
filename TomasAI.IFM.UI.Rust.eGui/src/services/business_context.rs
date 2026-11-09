use crate::backend::{contracts::*, transport::Backend};
use chrono::{Datelike, NaiveDate};
use rmpv::Value;

/// Business Context used by the business context component.
#[derive(Clone, Debug)]
pub struct BusinessContext {
    /// Value date associated with this business context.
    pub value_date: NaiveDate,
    /// Optional active value date; `None` represents an absent value.
    pub active_value_date: Option<NaiveDate>,
    /// As of utc associated with this business context.
    pub as_of_utc: chrono::DateTime<chrono::Utc>,
    /// Revision associated with this business context.
    pub revision: i64,
    /// Market state associated with this business context.
    pub market_state: i64,
    /// Whether allow development emulator is enabled or currently true.
    pub allow_development_emulator: bool,
}


impl BusinessContext {
    /// Whether the API confirms an open session, including overnight OffTrading hours.
    pub fn is_market_open(&self) -> bool {
        matches!(self.market_state, 1 | 2) && self.active_value_date == Some(self.value_date)
    }


    /// Whether the API has confirmed that live trading hours are active.
    pub fn is_live_trading(&self) -> bool {
        self.market_state == 2
    }


    /// Loads business context for business context.
    pub async fn load(backend: &Backend) -> Result<Self, String> {
        Self::load_with_transition(backend).await.map(|(context, _)| context)
    }


    /// Loads the authoritative session and its next scheduled transition together.
    pub async fn load_with_transition(
        backend: &Backend,
    ) -> Result<(Self, chrono::DateTime<chrono::Utc>), String> {
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
        let next_transition = timestamp(&session[6])?;
        if start.year() <= 1
            || end <= start
            || next_transition.year() <= 1
            || context.as_of_utc.year() <= 1
        {
            return Err("Invalid API market-session timing".into());
        }
        Ok((context, next_transition))
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


#[cfg(test)]
mod tests {
    use super::*;


    /// Overnight is an open data session; closed, unknown and inconsistent sessions remain paused.
    #[test]
    fn market_data_views_accept_both_open_states_only_with_the_matching_active_date() {
        let date = NaiveDate::from_ymd_opt(2026, 11, 9).unwrap();
        for (market_state, active_value_date, expected) in [
            (0, None, false),
            (1, Some(date), true),
            (2, Some(date), true),
            (1, None, false),
            (3, Some(date), false),
            (1, date.pred_opt(), false),
        ] {
            let context = BusinessContext {
                value_date: date,
                active_value_date,
                as_of_utc: "2026-11-09T00:00:00Z".parse().unwrap(),
                revision: 1,
                market_state,
                allow_development_emulator: false,
            };
            assert_eq!(context.is_market_open(), expected, "state={market_state}");
            assert_eq!(context.is_live_trading(), market_state == 2);
        }
    }
}
