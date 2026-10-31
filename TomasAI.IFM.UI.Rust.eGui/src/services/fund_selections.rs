//! Fund editor choices come from published lookup/product APIs. Labels never
//! become identities; unavailable stored values remain visible but unselectable.
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use crate::{backend::contract_view::Field, models::portfolio::Choice};
use rmpv::Value;
const LOOKUP: &str = "TomasAI.IFM.Domain.Reference.Shared.Lookups.LookupDefinitionReadModel";
const PRODUCT: &str =
    "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.TradeStrategySymbolReadModel";
#[derive(Clone)]
struct Lookup {
    id: i64,
    group: String,
    value: String,
    label: String,
    order: Option<i64>,
    enabled: bool,
}
impl Lookup {
    fn read(registry: &Registry, value: &Value) -> Result<Self, String> {
        let row = Field::new(registry, LOOKUP, value);
        let required = |key| -> Result<String, String> {
            Ok(row
                .field(key)?
                .string()
                .ok_or("Invalid lookup text")?
                .to_string())
        };
        Ok(Self {
            id: row
                .field("Id")?
                .integer()
                .ok_or("Invalid lookup identity")?,
            group: required("GroupName")?,
            value: required("InternalValue")?,
            label: required("DisplayName")?,
            order: row.field("DisplayOrder")?.integer(),
            enabled: row.field("IsEnabled")?.value.as_bool() == Some(true),
        })
    }
}
use std::collections::{BTreeMap, BTreeSet};
#[derive(Clone, Default)]
pub struct Selections {
    pub underlyings: Vec<String>,
    groups: BTreeMap<String, Vec<Lookup>>,
}
fn selectable(registry: &Registry, row: &Lookup) -> bool {
    if !row.enabled {
        return false;
    }
    match row.group.as_str() {
        "AssetTypes" => matches!(row.value.as_str(), "Futures" | "FuturesOption"),
        "Directions" | "MarketConditions" => {
            let name = if row.group == "Directions" {
                "MarketConditionDirection"
            } else {
                "MarketConditionType"
            };
            row.value != "Undefined" && registry.enum_value(&format!("TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model.{name}"), &row.value).is_ok()
        }
        _ => false,
    }
}
impl Selections {
    pub async fn load(
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<Self, String> {
        let mut output = Self::default();
        for group in ["AssetTypes", "Directions", "MarketConditions"] {
            let subject = ActorSubject::query("ReferenceQuery", "GetLookupDefinitions", "none");
            let body = Value::Array(vec![
                subject.wire(),
                Value::Array(vec![0.into(), Value::Array(vec!["none".into()])]),
                group.into(),
            ]);
            let reply = backend
                .request_until(&subject, &body, deadline, None)
                .await?;
            let rows = service_result(&reply)?
                .as_array()
                .filter(|rows| !rows.is_empty() && rows.len() <= 4096)
                .ok_or(format!(
                    "{group}: lookup definitions are missing or invalid"
                ))?;
            let mut rows = rows
                .iter()
                .map(|row| Lookup::read(registry, row))
                .collect::<Result<Vec<_>, _>>()?;
            let mut ids = BTreeSet::new();
            for row in &rows {
                if row.id <= 0
                    || row.group != group
                    || row.value.trim().is_empty()
                    || row.label.trim().is_empty()
                    || !ids.insert(row.value.clone())
                {
                    return Err(format!(
                        "{group}: lookup definitions are missing or invalid"
                    ));
                }
            }
            rows.sort_by_key(|row| (row.order, row.id));
            output.groups.insert(group.into(), rows);
        }
        let mut symbols = BTreeSet::new();
        for family in [1, 2] {
            let subject = ActorSubject::query("ReferenceQuery", "GetTradeStrategySymbols", "none");
            let body = Value::Array(vec![
                subject.wire(),
                Value::Array(vec![0.into(), Value::Array(vec!["none".into()])]),
                family.into(),
            ]);
            let reply = backend
                .request_until(&subject, &body, deadline, None)
                .await?;
            let rows = service_result(&reply)?
                .as_array()
                .filter(|rows| rows.len() <= 100_000)
                .ok_or("Invalid Databento product list")?;
            for wire in rows {
                let row = Field::new(registry, PRODUCT, wire);
                let required = |name| -> Result<&str, String> {
                    row.field(name)?
                        .string()
                        .filter(|value| !value.trim().is_empty())
                        .ok_or("Invalid Databento product metadata".into())
                };
                let symbol = required("Symbol")?;
                let exchange = required("Exchange")?;
                let currency = required("Currency")?;
                required("Description")?;
                if symbol != symbol.trim()
                    || exchange != exchange.trim()
                    || currency.len() != 3
                    || !currency.bytes().all(|value| value.is_ascii_uppercase())
                    || row.field("Id")?.integer().is_none_or(|id| id <= 0)
                {
                    return Err("Invalid Databento product metadata".into());
                }
                symbols.insert(symbol.to_string());
            }
        }
        if symbols.is_empty() {
            return Err("No current Databento underlying symbols are loaded. Refresh instrument definitions first".into());
        }
        output.underlyings = symbols.into_iter().collect();
        output
            .underlyings
            .sort_by(|a, b| a.encode_utf16().cmp(b.encode_utf16()));
        Ok(output)
    }
    pub(crate) fn choices(&self, registry: &Registry, group: &str) -> Result<Vec<Choice>, String> {
        Ok(self
            .groups
            .get(group)
            .ok_or("Missing Fund lookup group")?
            .iter()
            .map(|row| Choice {
                value: row.value.clone(),
                label: row.label.clone(),
                enabled: selectable(registry, row),
            })
            .collect())
    }
    pub fn validate(
        &self,
        registry: &Registry,
        underlyings: &[String],
        assets: &[String],
        directions: &[String],
        conditions: &[String],
    ) -> Result<(), String> {
        for (label, values, available) in [
            ("Underlyings", underlyings, self.underlyings.clone()),
            ("Asset Types", assets, self.allowed(registry, "AssetTypes")?),
            (
                "Directions",
                directions,
                self.allowed(registry, "Directions")?,
            ),
            (
                "Market Conditions",
                conditions,
                self.allowed(registry, "MarketConditions")?,
            ),
        ] {
            let mut seen = BTreeSet::new();
            if values
                .iter()
                .any(|v| !available.contains(v) || !seen.insert(v))
            {
                return Err(format!(
                    "{label} contains unavailable or duplicate selections. Select values from the current list"
                ));
            }
        }
        Ok(())
    }
    fn allowed(&self, registry: &Registry, group: &str) -> Result<Vec<String>, String> {
        Ok(self
            .groups
            .get(group)
            .ok_or("Missing Fund lookup group")?
            .iter()
            .filter(|row| selectable(registry, row))
            .map(|row| row.value.clone())
            .collect())
    }
}
