//! Canonical application mark queries. They retain detached numeric views only.
use super::*;
use crate::{
    latest::{Asset, price_policy},
    option_chain::qualification::Decimal,
};
impl MarketData {
    /// Reads one current native mark. The boolean distinguishes unmapped contract
    /// identity from an accepted contract whose current quote/trade is unavailable.
    ///
    /// # Errors
    /// Invalid policy, interrupted source state or the existing crossed-option error.
    pub fn contract_price(
        &self,
        contract: &str,
        options: bool,
        maximum_age: i64,
    ) -> Result<(bool, Option<Decimal>), String> {
        if contract.is_empty() || contract.len() > 1024 || maximum_age <= 0 {
            return Err("Invalid native price request".into());
        }
        let lanes: &[Lane] = if options {
            &[Lane::OptionsGlbx]
        } else {
            &[Lane::FuturesGlbx, Lane::FuturesXcbf]
        };
        for &lane in lanes {
            let binding = if options {
                self.options
                    .factory
                    .view()
                    .and_then(|v| {
                        v.registrations
                            .iter()
                            .find(|r| r.contract == contract && r.asset == Asset::Option)
                            .map(|r| r.instrument)
                    })
                    .or_else(|| {
                        self.options.control.intent().and_then(|i| {
                            i.source
                                .registrations
                                .iter()
                                .find(|r| r.contract == contract && r.asset == Asset::Option)
                                .map(|r| r.instrument)
                        })
                    })
            } else {
                self.futures
                    .get(&lane)
                    .and_then(|f| f.factory.registrations())
                    .and_then(|rs| {
                        rs.iter()
                            .find(|r| r.contract == contract && r.asset == Asset::Futures)
                            .map(|r| r.instrument)
                    })
            };
            let Some(binding) = binding else {
                continue;
            };
            let before = self.health(lane).status();
            if before.phase != Phase::Running || !self.has_desired(lane) {
                return Ok((true, None));
            }
            let queries = if options {
                self.options_current().map(|v| v.queries.clone())
            } else {
                self.futures.get(&lane).and_then(|f| f.factory.queries())
            };
            let Some(raw) = queries
                .map(|q| q.raw_prices(binding))
                .transpose()?
                .flatten()
            else {
                return Ok((true, None));
            };
            let price = price_policy::calculate(raw, options, self.clock_ticks()?, maximum_age)?;
            let after = self.health(lane).status();
            return Ok((
                true,
                if before.ticket == after.ticket && after.phase == Phase::Running {
                    price
                } else {
                    None
                },
            ));
        }
        Ok((false, None))
    }
    /// Reads the single native-selected front futures contract for strategy
    /// coverage. This never chooses a local UI date or creates an underlying lease.
    ///
    /// # Errors
    /// Missing/ambiguous root authority or interrupted exact price state.
    pub fn root_price(&self, root: &str) -> Result<Option<Decimal>, String> {
        let mut ids = Vec::new();
        for future in self.futures.values() {
            if let Some(models) = future.factory.public_models() {
                for model in models.iter() {
                    let f = model
                        .as_array()
                        .ok_or("Malformed current futures reference")?;
                    if f.get(2).and_then(rmpv::Value::as_str) == Some(root)
                        && f.get(9).and_then(rmpv::Value::as_bool) == Some(true)
                    {
                        ids.push(
                            f[0].as_str()
                                .ok_or("Missing current futures identity")?
                                .to_owned(),
                        );
                    }
                }
            }
        }
        if ids.len() != 1 {
            return Err("Current native strategy root is missing or ambiguous".into());
        }
        Ok(self.contract_price(&ids[0], false, 9_000_000_000)?.1)
    }
}

impl MarketData {
    /// Copies the exact current canonical/provider binding. This is query data,
    /// never a source handle, subscription token or replacement acknowledgement.
    ///
    /// # Errors
    /// Interrupted native metadata encoding; unknown identities return nil.
    pub fn contract_binding(&self, contract: &str, options: bool) -> Result<rmpv::Value, String> {
        use crate::messaging::wire::guid;
        let lanes: &[Lane] = if options {
            &[Lane::OptionsGlbx]
        } else {
            &[Lane::FuturesGlbx, Lane::FuturesXcbf]
        };
        for &lane in lanes {
            let find = |rs: &[crate::dataset::live::Registration]| {
                rs.iter()
                    .find(|r| r.contract == contract && (r.asset == Asset::Option) == options)
                    .map(|r| (r.instrument, r.publisher))
            };
            let binding = if options {
                self.options
                    .factory
                    .view()
                    .and_then(|v| find(&v.registrations))
                    .or_else(|| {
                        self.options
                            .control
                            .intent()
                            .and_then(|i| find(&i.source.registrations))
                    })
            } else {
                self.futures
                    .get(&lane)
                    .and_then(|f| f.factory.registrations())
                    .and_then(|rs| find(&rs))
            };
            let Some((instrument, publisher)) = binding else {
                continue;
            };
            let Some(identity) = self.publication_identity(lane) else {
                return Ok(rmpv::Value::Nil);
            };
            return Ok(rmpv::Value::Array(vec![
                (lane as u8).into(),
                identity.value_date.into(),
                instrument.into(),
                publisher.into(),
                self.has_desired(lane).into(),
                (self.health(lane).status().phase == Phase::Running).into(),
                guid(identity.worker).into(),
                guid(identity.generation).into(),
            ]));
        }
        Ok(rmpv::Value::Nil)
    }
}

impl MarketData {
    /// Returns one canonical raw option observation with its exact native risk.
    /// Identity, generation and quote coherence are joined here, never in C#.
    /// Local ingestion time qualifies Greeks; the returned tick retains its true
    /// provider receipt time and local ingestion field independently.
    ///
    /// # Errors
    /// Interrupted source state or malformed internal copied calculation data.
    pub fn canonical_risk(
        &self,
        contract: &str,
        date: i32,
        trade: bool,
    ) -> Result<rmpv::Value, String> {
        use rmpv::Value;
        let binding = self.contract_binding(contract, true)?;
        let Some(fields) = binding.as_array() else {
            return Ok(Value::Nil);
        };
        if fields[1].as_i64() != Some(i64::from(date)) {
            return Ok(Value::Nil);
        }
        let instrument = u32::try_from(fields[2].as_u64().ok_or("Invalid native binding")?)
            .map_err(|_| "Invalid native binding")?;
        let publisher = u16::try_from(fields[3].as_u64().ok_or("Invalid native binding")?)
            .map_err(|_| "Invalid native binding")?;
        let Some(risk) = (if trade {
            self.trade_greeks(instrument, publisher)?
        } else {
            self.quote_greeks(instrument, publisher, self.clock_ticks()?)?
        }) else {
            return Ok(Value::Nil);
        };
        let Some(parts) = risk.as_array().filter(|p| p.len() == 3) else {
            return Err("Invalid native risk projection".into());
        };
        let result = if trade {
            risk.clone()
        } else {
            let Some(raw) = self.raw_prices(Lane::OptionsGlbx, contract, date)? else {
                return Ok(Value::Nil);
            };
            let raw = raw
                .as_array()
                .filter(|p| p.len() == 3)
                .ok_or("Invalid native raw projection")?;
            let Some(quote) = raw[1].as_array().filter(|q| q.len() == 12) else {
                return Ok(Value::Nil);
            };
            let priced = parts[0]
                .as_array()
                .filter(|q| q.len() == 9)
                .ok_or("Invalid native pricing quote")?;
            let equal = |left: &Value, right: &Value| {
                left.as_str()
                    .and_then(crate::reference::decimal::parse)
                    .zip(right.as_str().and_then(crate::reference::decimal::parse))
                    .is_some_and(|(a, b)| a.compare(b) == Some(std::cmp::Ordering::Equal))
            };
            let size = |v: &Value| {
                v.as_u64()
                    .map(|v| Value::from(v.to_string()))
                    .unwrap_or(Value::Nil)
            };
            if raw[2] != parts[2]
                || priced[0] != quote[0]
                || !equal(&priced[1], &quote[2])
                || !equal(&priced[2], &quote[5])
                || !equal(&priced[3], &size(&quote[3]))
                || !equal(&priced[4], &size(&quote[6]))
                || priced[5] != quote[9]
                || priced[6] != quote[11]
                || priced[7] != quote[8]
                || priced[8] != fields[7]
            {
                return Ok(Value::Nil);
            }
            Value::Array(vec![raw[1].clone(), parts[1].clone(), parts[2].clone()])
        };
        if self.contract_binding(contract, true)? != binding {
            return Ok(Value::Nil);
        }
        Ok(result)
    }
}

impl MarketData {
    /// Reads native publication-price or session-statistics state. These queries
    /// share streaming projections and preserve price suppression/continuity;
    /// raw last-trade prices must not be substituted for publication state.
    ///
    /// # Errors
    /// Invalid identity, interrupted source or failed native DTO projection.
    pub fn current_snapshot(
        &self,
        contract: &str,
        statistics: bool,
    ) -> Result<rmpv::Value, String> {
        use crate::messaging::events::Instrument;
        if contract.is_empty() || contract.len() > 1024 {
            return Err("Invalid native current-snapshot identity".into());
        }
        for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf] {
            let slot = self
                .futures
                .get(&lane)
                .ok_or("Unknown native futures lane")?;
            let before = self.health(lane).status();
            if before.phase != Phase::Running || !self.has_desired(lane) {
                continue;
            }
            let Some(registrations) = slot.factory.registrations() else {
                continue;
            };
            let Some(registration) = registrations.iter().find(|row| row.contract == contract)
            else {
                continue;
            };
            let Some(header) = slot.factory.identity() else {
                return Ok(rmpv::Value::Nil);
            };
            let Some((_, state)) = slot
                .factory
                .queries()
                .map(|queries| queries.publication_state(registration.instrument))
                .transpose()?
                .flatten()
            else {
                return Ok(rmpv::Value::Nil);
            };
            let date = time::Date::from_julian_day(header.value_date + 1721426)
                .map_err(|_| "Invalid native source ValueDate")?;
            let instrument = Instrument::new(registration.clone(), lane, date, date)?;
            let result = if statistics {
                state
                    .session
                    .map(|selected| instrument.statistics_snapshot(selected))
                    .transpose()?
            } else {
                state
                    .market
                    .map(|selected| instrument.price_snapshot(Some(selected)))
                    .transpose()?
            }
            .unwrap_or(rmpv::Value::Nil);
            let after = self.health(lane).status();
            if before.ticket != after.ticket
                || after.phase != Phase::Running
                || !self.has_desired(lane)
            {
                return Ok(rmpv::Value::Nil);
            }
            return Ok(result);
        }
        Ok(rmpv::Value::Nil)
    }
}

impl MarketData {
    /// Copies one individually monitored option ticker and joins optional Greeks
    /// against the exact publication-price observation in Rust. Repeated raw
    /// trades never replace a suppressed publication-price observation.
    ///
    /// # Errors
    /// Interrupted native copies or malformed internal typed projection.
    pub fn option_ticker(&self, contract: &str) -> Result<rmpv::Value, String> {
        use crate::messaging::{events::Instrument, wire::guid};
        use rmpv::Value;
        let Some(view) = self.options_current() else {
            return Ok(Value::Nil);
        };
        let Some(row) = view
            .registrations
            .iter()
            .find(|row| row.contract == contract && row.asset == Asset::Option)
        else {
            return Ok(Value::Nil);
        };
        if !view.monitored.contains(&row.instrument) {
            return Ok(Value::Nil);
        }
        let Some((physical, state)) = view.queries.publication_state(row.instrument)? else {
            return Ok(Value::Nil);
        };
        let Some(snapshot) = state.market else {
            return Ok(Value::Nil);
        };
        let date = view.header.value_date;
        let instrument =
            Instrument::new(row.clone(), Lane::OptionsGlbx, snapshot.date, snapshot.date)?;
        let market = instrument.price_snapshot(Some(snapshot))?;
        let mut market = market
            .as_array()
            .ok_or("Malformed native market snapshot")?
            .clone();
        let matches = |risk: &Value, trade: bool| -> bool {
            let Some(parts) = risk.as_array().filter(|parts| parts.len() == 3) else {
                return false;
            };
            if parts[2] != Value::from(guid(physical.to_le_bytes())) {
                return false;
            }
            let Some(raw) = parts[0].as_array() else {
                return false;
            };
            let Some(selected) = market[if trade { 6 } else { 5 }].as_array() else {
                return false;
            };
            if trade {
                raw.len() == 7
                    && selected.len() == 10
                    && raw[4] == selected[2]
                    && raw[5] == selected[3]
                    && raw[6] == selected[4]
            } else {
                raw.len() == 12
                    && selected.len() == 9
                    && raw[8] == selected[6]
                    && raw[9] == selected[7]
                    && raw[10] == selected[8]
            }
        };
        let quote = self.canonical_risk(contract, date, false)?;
        let greeks = if matches(&quote, false) {
            quote.as_array().unwrap()[1].clone()
        } else {
            let trade = self.canonical_risk(contract, date, true)?;
            if matches(&trade, true) {
                trade.as_array().unwrap()[1].clone()
            } else {
                Value::Nil
            }
        };
        if let Some(trade) = market[6].as_array() {
            market[6] = Value::Array(trade[..5].to_vec());
        }
        if view.queries.generation()? != Some(physical)
            || self
                .options_current()
                .is_none_or(|next| next.header.generation != view.header.generation)
        {
            return Ok(Value::Nil);
        }
        Ok(Value::Array(vec![Value::Array(market), greeks]))
    }
}
