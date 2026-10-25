//! Deterministic provider-to-reference draft preparation. Provider evidence is
//! copied exactly; review qualification stays with the backend convention path.
//! `at` must come from the API session, never the workstation clock.
use crate::backend::{
    identity::{decimal_to_legacy_double, option_contract_id},
    schema::Registry,
};
use chrono::{DateTime, FixedOffset, NaiveDate, Utc};
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
pub const FUTURE: &str =
    "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel";
pub const OPTION: &str =
    "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionContractReadModel";
pub const DEFINITION: &str =
    "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.InstrumentDefinitionSelection";
fn text<'a>(row: &'a Json, key: &str) -> &'a str {
    row[key].as_str().unwrap_or_default()
}
fn instant(row: &Json, key: &str) -> Result<DateTime<FixedOffset>, String> {
    DateTime::parse_from_rfc3339(text(row, key)).map_err(|_| format!("Missing or invalid {key}"))
}
/// The caller supplies the exchange-local expiry date obtained through the
/// timezone adapter. Keeping conversion explicit makes date boundaries testable.
pub fn prepare(
    registry: &Registry,
    source: &Value,
    zone: &str,
    at: DateTime<Utc>,
    local_expiry: NaiveDate,
    underlying: Option<&Value>,
    original: Option<&Value>,
) -> Result<Value, String> {
    let source_wire = source;
    let source = registry.project(DEFINITION, source)?;
    let snapshot = uuid::Uuid::parse_str(text(&source, "SnapshotId"))
        .map_err(|_| "Invalid definition snapshot")?;
    let expiration = instant(&source, "ExpirationUtc")?;
    if snapshot.is_nil()
        || source["InstrumentId"].as_u64().unwrap_or(0) == 0
        || source["PublisherId"].as_u64().unwrap_or(0) == 0
        || source["Deleted"] == true
        || expiration <= at
        || (!source["ActivationUtc"].is_null() && instant(&source, "ActivationUtc")? > at)
        || text(&source, "RawSymbol").trim().is_empty()
        || text(&source, "DefinitionDigest").encode_utf16().count() != 64
        || text(&source, "RawDefinitionReference").trim().is_empty()
    {
        return Err("A current, complete provider definition is required".into());
    }
    if !crate::backend::time_zone::is_known(zone) {
        return Err("Select a known exchange time zone".into());
    }
    let future = text(&source, "InstrumentClass") == "F";
    let kind = if future { FUTURE } else { OPTION };
    let mut fields = BTreeMap::new();
    let symbol;
    if future {
        symbol = text(&source, "Root").to_string();
        if symbol.is_empty() || !symbol.bytes().all(|c| c.is_ascii_alphanumeric()) {
            return Err("A futures definition is required".into());
        }
        fields.insert(
            "ContractId".into(),
            json!(format!("{}{}", symbol, local_expiry.format("%Y%m%d"))),
        );
        fields.insert("LastTradeDate".into(), json!(local_expiry.to_string()));
        fields.insert("SecurityType".into(), json!("FUT"));
        fields.insert("OnTheRun".into(), json!(false));
        fields.insert("Rollover".into(), json!(false));
    } else {
        let call = match text(&source, "InstrumentClass") {
            "C" => true,
            "P" => false,
            _ => return Err("A call or put with an exact strike is required".into()),
        };
        let strike = text(&source, "Strike");
        let under = registry.project(
            FUTURE,
            underlying.ok_or("Select the exact provider underlying future")?,
        )?;
        if text(&under, "SecurityType") != "FUT"
            || under["SchemaVersion"] != 1
            || under["InstrumentId"] != source["UnderlyingInstrumentId"]
            || source["UnderlyingInstrumentId"].as_u64().unwrap_or(0) == 0
            || under["Dataset"] != source["Dataset"]
            || under["PublisherId"] != source["PublisherId"]
            || instant(&under, "ExpirationUtc")? < expiration
        {
            return Err(
                "Import/select the exact provider underlying future before saving the option"
                    .into(),
            );
        }
        symbol = text(&under, "Symbol").into();
        fields.insert(
            "ContractId".into(),
            json!(option_contract_id(
                &symbol,
                local_expiry,
                if call { "Call" } else { "Put" },
                strike
            )?),
        );
        fields.insert("ContractMonth".into(), json!(local_expiry.to_string()));
        fields.insert("SecurityType".into(), json!("FOP"));
        fields.insert("StrikePriceDecimal".into(), json!(strike));
        fields.insert(
            "StrikePrice".into(),
            json!(decimal_to_legacy_double(strike)?),
        );
        fields.insert(
            "OptionType".into(),
            json!(if call { "Call" } else { "Put" }),
        );
        fields.insert("UnderlyingContractId".into(), under["ContractId"].clone());
        fields.insert("UnderlyingAssetType".into(), json!("Futures"));
        fields.insert(
            "UnderlyingInstrumentId".into(),
            source["UnderlyingInstrumentId"].clone(),
        );
        fields.insert("UnderlyingPublisherId".into(), under["PublisherId"].clone());
        fields.insert(
            "OptionRight".into(),
            json!(if call { "Call" } else { "Put" }),
        );
    }
    fields.insert("Symbol".into(), json!(symbol));
    for member in [
        "Currency",
        "Exchange",
        "Dataset",
        "PublisherId",
        "InstrumentId",
        "RawSymbol",
        "DefinitionTimestampUtc",
        "DefinitionDigest",
        "RawDefinitionReference",
        "ExpirationUtc",
        "TickSize",
    ] {
        fields.insert(member.into(), source[member].clone());
    }
    for member in ["Description", "LocalSymbol"] {
        fields.insert(member.into(), source["RawSymbol"].clone());
    }
    fields.insert(
        "Multiplier".into(),
        json!(source["Multiplier"].as_str().unwrap_or_default()),
    );
    fields.insert("MultiplierValue".into(), source["Multiplier"].clone());
    fields.insert("PriceScale".into(), json!("1"));
    fields.insert("SchemaVersion".into(), json!(1));
    fields.insert("ReviewState".into(), json!("Draft"));
    fields.insert("ExchangeTimeZoneId".into(), json!(zone));
    if let Some(wire) = original {
        let old = registry.project(kind, wire)?;
        if old["ReviewState"] == "Reviewed" {
            return Err("Draft import cannot replace an existing contract qualification; use the convention editor".into());
        }
        if old["SchemaVersion"].as_i64().unwrap_or(0) > 0
            && ["Dataset", "PublisherId", "InstrumentId"]
                .iter()
                .any(|key| old[key] != source[key])
        {
            return Err(
                "Mapping cannot replace an existing provider instrument; add a new contract".into(),
            );
        }
        let mut immutable = vec![
            "Symbol",
            if future {
                "LastTradeDate"
            } else {
                "ContractMonth"
            },
        ];
        if !future {
            immutable.push("OptionType");
            let old_strike = crate::backend::identity::exact_option_strike(&old)?;
            if crate::backend::identity::option_strike(&old_strike)?.0
                != crate::backend::identity::option_strike(
                    fields["StrikePriceDecimal"]
                        .as_str()
                        .ok_or("Missing exact strike")?,
                )?
                .0
            {
                return Err(
                    "Mapping changes the contract identity; add a new contract instead".into(),
                );
            }
        }
        if immutable.iter().any(|key| old[key] != fields[*key]) {
            return Err("Mapping changes the contract identity; add a new contract instead".into());
        }
        for member in [
            "MappingVersion",
            "EvidenceId",
            "CalendarVersion",
            "EffectiveFromUtc",
            "EffectiveUntilUtc",
            "LastTradingUtc",
            "SettlementStyle",
        ] {
            fields.insert(
                member.into(),
                if old[member].is_null()
                    && ["MappingVersion", "EvidenceId", "CalendarVersion"].contains(&member)
                {
                    json!("")
                } else {
                    old[member].clone()
                },
            );
        }
        fields.insert("ContractId".into(), old["ContractId"].clone());
        if future {
            for member in ["OnTheRun", "Rollover"] {
                fields.insert(member.into(), old[member].clone());
            }
        } else {
            for member in [
                "ExerciseStyle",
                "PremiumStyle",
                "PremiumTickRule",
                "TickRuleVersion",
                "DayCount",
                "ExerciseCutoffUtc",
                "ExerciseResultContractId",
            ] {
                fields.insert(
                    member.into(),
                    if member == "TickRuleVersion" && old[member].is_null() {
                        json!("")
                    } else {
                        old[member].clone()
                    },
                );
            }
        }
    }
    let empty = registry.encode(kind, &BTreeMap::new())?;
    let mut mapped = registry.patch(kind, &empty, &fields)?;
    registry.copy_members(
        kind,
        &mut mapped,
        DEFINITION,
        source_wire,
        &["ExpirationUtc"],
    )?;
    // DefinitionTimestampUtc is nonnullable in a selection and nullable in the
    // reference. Its present payload is identical; preserve its original offset.
    let timestamp = source_wire
        .as_array()
        .and_then(|v| v.get(15))
        .ok_or("Missing definition instant")?
        .clone();
    let Value::Array(values) = &mut mapped else {
        return Err("Invalid mapped model".into());
    };
    values[if future { 17 } else { 18 }] = timestamp;
    if let Some(original) = original {
        registry.copy_members(
            kind,
            &mut mapped,
            kind,
            original,
            &["EffectiveFromUtc", "EffectiveUntilUtc", "LastTradingUtc"],
        )?;
        if !future {
            registry.copy_members(kind, &mut mapped, kind, original, &["ExerciseCutoffUtc"])?;
        }
    }
    Ok(mapped)
}

/// Match the existing mapping-preview display document, including its public
/// derived identity fields. This is presentation only; commands use `Value`.
pub fn preview_document(registry: &Registry, kind: &str, wire: &Value) -> Result<Json, String> {
    let row = registry.project(kind, wire)?;
    let future = kind == FUTURE;
    let mut document = row
        .as_object()
        .ok_or("Invalid preview contract")?
        .iter()
        .map(|(key, value)| {
            let mut chars = key.chars();
            (
                format!(
                    "{}{}",
                    chars.next().unwrap().to_ascii_lowercase(),
                    chars.as_str()
                ),
                value.clone(),
            )
        })
        .collect::<serde_json::Map<_, _>>();
    for (key, member) in [
        ("reviewState", "ReferenceReviewState"),
        ("settlementStyle", "ReferenceSettlementStyle"),
        ("underlyingAssetType", "ReferenceAssetType"),
        ("optionRight", "ReferenceOptionRight"),
        ("exerciseStyle", "ReferenceExerciseStyle"),
        ("premiumStyle", "ReferencePremiumStyle"),
        ("premiumTickRule", "ReferencePremiumTickRule"),
        ("dayCount", "ReferenceDayCount"),
    ] {
        if let Some(value) = document.get_mut(key) {
            let name = value.as_str().ok_or("Invalid preview enum")?;
            *value = json!(registry.enum_value(
                &format!("TomasAI.IFM.Domain.MarketData.Shared.ViewModels.{member}"),
                name
            )?);
        }
    }
    let offsets = if future {
        vec![
            ("definitionTimestampUtc", 17),
            ("expirationUtc", 20),
            ("lastTradingUtc", 21),
            ("effectiveFromUtc", 30),
            ("effectiveUntilUtc", 31),
        ]
    } else {
        vec![
            ("definitionTimestampUtc", 18),
            ("expirationUtc", 21),
            ("lastTradingUtc", 22),
            ("effectiveFromUtc", 31),
            ("effectiveUntilUtc", 32),
            ("exerciseCutoffUtc", 38),
        ]
    };
    for (key, index) in offsets {
        let value = &wire.as_array().ok_or("Invalid preview model")?[index];
        if !value.is_nil() {
            document.insert(
                key.into(),
                json!(crate::backend::contracts::reference_offset(value)?),
            );
        }
    }
    if future {
        document.insert("id".into(),json!({"contractId":row["ContractId"],"symbol":row["Symbol"],"maturityDate":row["LastTradeDate"],"isVxContract":row["Symbol"]=="VX"}));
    } else {
        let strike = crate::backend::identity::option_strike(text(&row, "StrikePriceDecimal"))?.0;
        let date = text(&row, "ContractMonth");
        let year = date
            .get(..4)
            .ok_or("Invalid preview expiry")?
            .parse::<i32>()
            .map_err(|_| "Invalid preview year")?;
        document.insert("id".into(),json!({"contractId":row["ContractId"],"isEmpty":false,"isValid":true,"symbol":row["Symbol"],"maturityDate":format!("{date}T00:00:00.0000000"),"optionType":registry.enum_value("TomasAI.IFM.Domain.MarketData.Shared.OptionType",text(&row,"OptionType"))?,"strikePrice":strike}));
        document.insert(
            "entityId".into(),
            json!({"contractId":row["ContractId"],"year":year,"isValid":true}),
        );
    }
    document.insert("isValid".into(), json!(true));
    Ok(Json::Object(document))
}
