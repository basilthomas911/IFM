//! Detached Blotter selections. Only the raw backend chain may supply selected
//! legs. Display retention cannot make a missing quote eligible for execution.
use super::{
    broker_preview, option_chain,
    option_selection::{self, Contract},
};
use crate::backend::contracts::ExactDecimal;
use serde_json::Value as Json;

#[derive(Clone, Debug)]
pub struct Selected {
    pub quote: broker_preview::Leg,
    pub role: String,
    pub quote_at: Json,
    pub frozen: bool,
}
impl Selected {
    fn from_contract(
        row: &Json,
        role: &str,
        multiplier: &ExactDecimal,
        frozen: bool,
    ) -> Result<Self, String> {
        let c = Contract::from_backend(row)?;
        let decimal = |key: &str| -> Result<Option<ExactDecimal>, String> {
            if row[key].is_null() {
                Ok(None)
            } else {
                ExactDecimal::parse(
                    &row[key]
                        .as_str()
                        .map(str::to_string)
                        .unwrap_or_else(|| row[key].to_string()),
                )
                .map(Some)
            }
        };
        Ok(Self {
            quote: broker_preview::Leg {
                contract: c.id,
                side: if role.starts_with('+') { 1 } else { -1 },
                strike: Some(c.strike),
                is_call: c.is_call,
                is_future: false,
                bid: decimal("Bid")?,
                ask: decimal("Ask")?,
                delta: c.delta,
                vega: row["Vega"].as_f64(),
                multiplier: Some(multiplier.clone()),
            },
            role: role.into(),
            quote_at: row["QuoteAtUtc"].clone(),
            frozen,
        })
    }
}
#[derive(Default)]
pub struct Draft {
    pub raw: Option<Json>,
    pub display: Option<Json>,
    pub legs: Vec<Selected>,
    pub entered_price: Option<ExactDecimal>,
    pub manual: bool,
}
pub struct Settings<'a> {
    pub strategy: &'a str,
    pub call_delta: &'a ExactDecimal,
    pub put_delta: &'a ExactDecimal,
    pub call_width: &'a ExactDecimal,
    pub put_width: &'a ExactDecimal,
    pub multiplier: &'a ExactDecimal,
    pub live: bool,
    pub development: bool,
    pub editable: bool,
}
fn decimal(row: &Json, key: &str) -> Result<Option<ExactDecimal>, String> {
    if row[key].is_null() {
        return Ok(None);
    }
    ExactDecimal::parse(
        &row[key]
            .as_str()
            .map(str::to_string)
            .unwrap_or_else(|| row[key].to_string()),
    )
    .map(Some)
}
/// Delta targets are the hybrid's decimal percent divided by 100, then cast to
/// double. Round scale 29/30 ties to even before the conversion, as .NET does.
fn fraction(value: &ExactDecimal) -> f64 {
    let text = value.as_str();
    let negative = text.starts_with('-');
    let text = text.trim_start_matches('-');
    let (whole, frac) = text.split_once('.').unwrap_or((text, ""));
    let mut digits = format!("{whole}{frac}").into_bytes();
    let mut scale = frac.len() + 2;
    if scale > 28 {
        let keep = digits.len().saturating_sub(scale - 28);
        let first = *digits.get(keep).unwrap_or(&b'0');
        let round = first > b'5'
            || first == b'5'
                && (digits.iter().skip(keep + 1).any(|d| *d != b'0')
                    || keep > 0 && (digits[keep - 1] - b'0') % 2 == 1);
        digits.truncate(keep);
        if round {
            let mut i = digits.len();
            loop {
                if i == 0 {
                    digits.insert(0, b'1');
                    break;
                }
                i -= 1;
                if digits[i] < b'9' {
                    digits[i] += 1;
                    break;
                }
                digits[i] = b'0';
            }
        }
        scale = 28;
    }
    let mut digits = String::from_utf8(digits).unwrap();
    while digits.len() <= scale {
        digits.insert(0, '0');
    }
    digits.insert(digits.len() - scale, '.');
    let number: f64 = digits.parse().unwrap();
    if negative { -number } else { number }
}
pub(crate) fn validate(settings: &Settings) -> Result<(), String> {
    if ![
        "ShortIronCondor",
        "LongIronCondor",
        "PutCreditSpread",
        "PutDebitSpread",
        "CallCreditSpread",
        "CallDebitSpread",
    ]
    .contains(&settings.strategy)
    {
        return Err("Unsupported option strategy".into());
    }
    let zero = ExactDecimal::parse("0")?;
    let hundred = ExactDecimal::parse("100")?;
    let maximum = ExactDecimal::parse("100000")?;
    for value in [settings.call_delta, settings.put_delta] {
        if option_selection::compare(value, &zero).is_le()
            || option_selection::compare(value, &hundred).is_gt()
        {
            return Err("Deltas must be positive and no greater than 100".into());
        }
    }
    for value in [settings.call_width, settings.put_width] {
        if option_selection::compare(value, &zero).is_le()
            || option_selection::compare(value, &maximum).is_gt()
        {
            return Err("Widths must be positive and no greater than 100000".into());
        }
    }
    Ok(())
}
impl Draft {
    pub fn accept(&mut self, incoming: Json, settings: &Settings) -> Result<(), String> {
        validate(settings)?;
        let rows = incoming["Contracts"]
            .as_array()
            .ok_or("Missing raw option contracts")?;
        let contracts = rows
            .iter()
            .map(Contract::from_backend)
            .collect::<Result<Vec<_>, _>>()?;
        let underlying = decimal(&incoming, "UnderlyingPrice")?;
        let display = option_chain::merge_display(self.display.as_ref(), &incoming)?;
        let mut next = vec![];
        if !self.manual && settings.editable {
            for is_call in [true, false] {
                if !settings.strategy.ends_with("IronCondor")
                    && is_call != settings.strategy.starts_with("Call")
                {
                    continue;
                }
                let credit = ["ShortIronCondor", "PutCreditSpread", "CallCreditSpread"]
                    .contains(&settings.strategy);
                let width = if is_call {
                    settings.call_width
                } else {
                    settings.put_width
                };
                let delta = if is_call {
                    settings.call_delta
                } else {
                    settings.put_delta
                };
                let frozen = !settings.live
                    && settings.development
                    && incoming["WindowMethod"]
                        .as_str()
                        .unwrap_or_default()
                        .starts_with("Frozen emulator preview");
                let side: Vec<_> = contracts
                    .iter()
                    .filter(|c| c.is_call == is_call)
                    .cloned()
                    .collect();
                for short in option_selection::short_candidates(
                    &contracts,
                    is_call,
                    fraction(delta),
                    underlying.as_ref(),
                    frozen,
                ) {
                    let Some(wing) = option_selection::wing(
                        &side,
                        &short.strike,
                        width,
                        if is_call { credit } else { !credit },
                    ) else {
                        continue;
                    };
                    for (id, role) in [
                        (&short.id, if is_call { "-SC" } else { "-SP" }),
                        (&wing.id, if is_call { "+LC" } else { "+LP" }),
                    ] {
                        let row = rows
                            .iter()
                            .find(|r| r["ContractId"].as_str() == Some(id))
                            .ok_or("Selected contract is missing")?;
                        next.push(Selected::from_contract(
                            row,
                            role,
                            settings.multiplier,
                            !settings.live,
                        )?);
                    }
                    break;
                }
            }
            if self
                .legs
                .iter()
                .map(|l| (&l.quote.contract, &l.role))
                .ne(next.iter().map(|l| (&l.quote.contract, &l.role)))
            {
                self.entered_price = None;
            }
        } else {
            for leg in &self.legs {
                next.push(
                    if let Some(row) = rows.iter().find(|r| r["ContractId"] == leg.quote.contract) {
                        Selected::from_contract(
                            row,
                            &leg.role,
                            settings.multiplier,
                            !settings.live,
                        )?
                    } else {
                        leg.clone()
                    },
                );
            }
        }
        self.legs = next;
        self.raw = Some(incoming);
        self.display = Some(display);
        Ok(())
    }
    pub fn clear(&mut self) {
        self.legs.clear();
        self.manual = true;
        self.entered_price = None;
    }
    pub fn remove(&mut self, id: &str) {
        self.legs.retain(|l| l.quote.contract != id);
        self.manual = true;
        self.entered_price = None;
    }
    pub fn select(&mut self, id: &str, role: &str, settings: &Settings) -> Result<(), String> {
        validate(settings)?;
        if !settings.editable {
            return Err("Market selection is read-only".into());
        }
        if !["-SC", "+LC", "-SP", "+LP"].contains(&role) {
            return Err("Invalid leg role".into());
        }
        let row = self
            .raw
            .as_ref()
            .and_then(|r| r["Contracts"].as_array())
            .and_then(|r| r.iter().find(|r| r["ContractId"] == id))
            .ok_or("Reload option chains")?;
        let c = Contract::from_backend(row)?;
        if c.is_call != role.ends_with('C')
            || !settings.strategy.ends_with("IronCondor")
                && c.is_call != settings.strategy.starts_with("Call")
        {
            return Err("Contract side differs from selected role or vertical strategy".into());
        }
        let required = if settings.strategy.ends_with("IronCondor") {
            4
        } else {
            2
        };
        if self.legs.len() >= required
            || self
                .legs
                .iter()
                .any(|l| l.quote.contract == id || l.role == role)
            || self
                .legs
                .iter()
                .filter(|l| l.quote.is_call == c.is_call)
                .count()
                >= 2
        {
            return Err("Remove a selected leg before selecting another contract".into());
        }
        self.legs.push(Selected::from_contract(
            row,
            role,
            settings.multiplier,
            !settings.live,
        )?);
        if self.legs.len() == required {
            for call in [true, false] {
                let mut indices: Vec<_> = (0..self.legs.len())
                    .filter(|i| self.legs[*i].quote.is_call == call)
                    .collect();
                if indices.len() != 2 {
                    continue;
                }
                indices.sort_by(|a, b| {
                    option_selection::compare(
                        self.legs[*a].quote.strike.as_ref().unwrap(),
                        self.legs[*b].quote.strike.as_ref().unwrap(),
                    )
                });
                let low_long = match settings.strategy {
                    "ShortIronCondor" => !call,
                    "LongIronCondor" => call,
                    "PutCreditSpread" | "CallDebitSpread" => true,
                    _ => false,
                };
                for (position, index) in indices.into_iter().enumerate() {
                    let long = if position == 0 { low_long } else { !low_long };
                    let leg = &mut self.legs[index];
                    leg.role = format!(
                        "{}{}",
                        if long { "+L" } else { "-S" },
                        if call { "C" } else { "P" }
                    );
                    leg.quote.side = if long { 1 } else { -1 };
                }
            }
        }
        self.manual = true;
        self.entered_price = None;
        Ok(())
    }
}
