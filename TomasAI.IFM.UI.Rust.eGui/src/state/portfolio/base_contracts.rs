//! Detached Fund base-contract editor. Saving changes defaults through the owning actor while preserving mandate bindings.
use super::editors::{editor, input};
use super::*;
use crate::models::portfolio::BaseContractNode;
use crate::services::business_context::BusinessContext;

impl PortfolioController {
    /// Publishes contract leaves without exposing mutable command state to the renderer.
    pub(super) fn base_contract_nodes(
        &self,
        registry: &Registry,
    ) -> Result<Vec<BaseContractNode>, String> {
        let Some(wire) = &self.data.base_contracts else {
            return Ok(vec![]);
        };
        let row = registry.project(&kind("FundBaseContractsReadModel"), wire)?;
        let mut nodes = vec![];
        for contract in row["Contracts"]
            .as_array()
            .ok_or("Invalid Fund contracts")?
        {
            nodes.push(BaseContractNode {
                symbol: text(&contract["Symbol"]),
                contract_id: text(&contract["ContractId"]),
                description: text(&contract["Description"]),
            });
        }
        for rule in row["Defaults"].as_array().ok_or("Invalid Fund defaults")? {
            let symbol = text(&rule["Symbol"]);
            if !nodes.iter().any(|node| node.symbol == symbol) {
                nodes.push(BaseContractNode {
                    symbol,
                    description: "Contracts are not initialized or qualified in IFM. Save Base Contracts to initialize them from Databento.".into(),
                    ..Default::default()
                });
            }
        }
        Ok(nodes)
    }

    /// Opens saved selections in a detached symbol/forward-cycle working copy.
    pub(super) fn open_base_contracts(&mut self, registry: &Registry) -> Result<Dialog, String> {
        let row = registry.project(
            &kind("FundBaseContractsReadModel"),
            self.data
                .base_contracts
                .as_ref()
                .ok_or("Load Fund base contracts first")?,
        )?;
        let defaults = row["Defaults"]
            .as_array()
            .ok_or("Invalid saved Fund defaults")?;
        let mut fields = vec![input(
            "BaseAsset",
            "Base Asset:",
            "Futures",
            "choice",
            true,
            None,
        )];
        let mut seen = std::collections::BTreeSet::new();
        for product in row["Symbols"]
            .as_array()
            .ok_or("Invalid Databento product list")?
        {
            let symbol = text(&product["Symbol"]);
            if !seen.insert(symbol.clone()) {
                continue;
            }
            let saved = defaults
                .iter()
                .find(|rule| rule["Symbol"].as_str() == Some(&symbol));
            fields.push(input(
                &format!("selected.{symbol}"),
                &symbol,
                saved.is_some(),
                "checkbox",
                true,
                Some(vec![Choice {
                    label: text(&product["Description"]),
                    ..Default::default()
                }]),
            ));
            fields.push(input(
                &format!("cycles.{symbol}"),
                "Forward cycles",
                saved
                    .map(|rule| text(&rule["ForwardCycles"]))
                    .unwrap_or_else(|| "2".into()),
                "number",
                true,
                None,
            ));
        }
        self.editing = Some((
            "FundMandateReadModel".into(),
            self.data.fund.clone().ok_or("Select a Fund")?,
        ));
        let mut dialog = editor(
            "base-contracts",
            "Fund Base Contracts",
            660,
            460,
            100,
            fields,
            "Save",
        );
        dialog.status = text(&row["Status"]);
        Ok(dialog)
    }

    /// Revalidates selected provider symbols and preserves all other Fund permissions and audit data.
    pub(super) fn base_contract_mandate(
        &self,
        registry: &Registry,
        original: &Value,
        intent: &Json,
        _context: &BusinessContext,
    ) -> Result<Value, String> {
        if field(intent, "BaseAsset") != "Futures" {
            return Err("Equities is not implemented".into());
        }
        let catalog = registry.project(
            &kind("FundBaseContractsReadModel"),
            self.data
                .base_contracts
                .as_ref()
                .ok_or("Reload Fund contracts")?,
        )?;
        let products = catalog["Symbols"]
            .as_array()
            .ok_or("Invalid Databento product list")?;
        let fields = intent["fields"].as_object().ok_or("Missing Fund inputs")?;
        let mut rules = vec![];
        let mut symbols = std::collections::BTreeSet::new();
        for (key, selected) in fields {
            if selected.as_str() != Some("true") {
                continue;
            }
            let Some(symbol) = key.strip_prefix("selected.") else {
                continue;
            };
            if !products
                .iter()
                .any(|row| row["Symbol"].as_str() == Some(symbol))
            {
                return Err(format!(
                    "{symbol} is not in the published Databento Futures catalog"
                ));
            }
            let cycles = fields
                .get(&format!("cycles.{symbol}"))
                .and_then(Json::as_str)
                .and_then(|v| v.parse::<i32>().ok())
                .filter(|v| (0..=120).contains(v))
                .ok_or("Forward cycles must be between 0 and 120")?;
            symbols.insert(symbol.to_string());
            rules.push(registry.encode(
                &kind("FundBaseContractRule"),
                &BTreeMap::from([
                    ("BaseAsset".into(), "Futures".into()),
                    ("Symbol".into(), symbol.into()),
                    ("ForwardCycles".into(), cycles.into()),
                ]),
            )?);
        }
        if rules.is_empty() {
            return Err("Select at least one Futures symbol".into());
        }
        let prior = registry.project(&kind("FundMandateReadModel"), original)?;
        for symbol in prior["UnderlyingUniverse"]
            .as_array()
            .ok_or("Invalid Fund underlying universe")?
        {
            symbols.insert(
                symbol
                    .as_str()
                    .ok_or("Invalid underlying symbol")?
                    .to_string(),
            );
        }
        crate::services::parameters::overlay_type(
            registry,
            &kind("FundMandateReadModel"),
            original,
            &BTreeMap::from([
                ("SchemaVersion".into(), 4.into()),
                ("BaseContracts".into(), Value::Array(rules)),
                (
                    "UnderlyingUniverse".into(),
                    Value::Array(symbols.into_iter().map(Value::from).collect()),
                ),
            ]),
        )
    }
}
