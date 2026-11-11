//! Brief operator-facing explanations of recorded pipeline decisions.
use super::StageHeader;

/// Explains a stage without exposing continuation enums, stack traces or timestamp diagnostics.
pub(super) fn stage_tooltip(title: &str, state: &str, stage: &StageHeader) -> String {
    let (decision, reason) = if let Some(failure) = &stage.failure
        && state != "No Trade"
    {
        ("Error", brief(&failure.error_message))
    } else {
        let reason = reasons(&stage.decision_reasons)
            .or_else(|| {
                stage
                    .failure
                    .as_ref()
                    .map(|failure| brief(&failure.error_message))
            })
            .unwrap_or_else(|| match state {
                "Processing" => format!("Waiting for {title} to finish."),
                "Continued" => match title {
                    "Regime Discovery" => {
                        "The market regime assessment completed; the workflow can continue.".into()
                    }
                    "Market Condition" => {
                        "Market conditions allow the workflow to continue.".into()
                    }
                    "Trade Selection" => "An eligible trading strategy was selected.".into(),
                    "Order Composition" => "An eligible order was composed.".into(),
                    "Risk Management" => "Risk checks approved the order handoff.".into(),
                    _ => "This stage completed; the workflow can continue.".into(),
                },
                _ => "No decision reason was recorded. Open Details for the available evidence."
                    .into(),
            });
        (
            if state == "Continued" {
                "Continue"
            } else {
                state
            },
            reason,
        )
    };
    format!("{title}: {decision}\nReason: {reason}")
}

/// Keeps tooltips short even when a backend failure carries multiline diagnostics.
fn brief(text: &str) -> String {
    let line = text
        .lines()
        .find(|line| !line.trim().is_empty())
        .unwrap_or("No explanation was recorded.")
        .trim();
    let mut short: String = line.chars().take(220).collect();
    if line.chars().count() > 220 {
        short.push('…');
    }
    short
}

/// Translates recorded reasons without inferring a new trading decision.
fn reasons(codes: &[String]) -> Option<String> {
    let mut descriptions = Vec::new();
    for code in codes.iter().filter(|code| !code.trim().is_empty()) {
        let text = description(code);
        if !descriptions.contains(&text) {
            descriptions.push(text);
        }
        if descriptions.len() == 3 {
            break;
        }
    }
    (!descriptions.is_empty()).then(|| brief(&descriptions.join(" ")))
}

/// Maps common decision codes to plain English; unfamiliar codes remain readable and visible.
fn description(code: &str) -> String {
    if let Some(observation) = code.strip_prefix("MC.ASSESSMENT.") {
        if let Some((source, status)) = observation.rsplit_once('.') {
            let name = match source {
                "REFERENCEQUOTE" => Some("futures quote"),
                "LASTTRADE" => Some("latest futures trade"),
                "NORMALIZEDMOVEMENT" => Some("one-minute ATR observation"),
                "VOLATILITYCHANGE" => Some("volatility-change observation"),
                "FEEDHEALTH" => Some("feed-health observation"),
                "SESSIONCALENDAR" => Some("trading-session information"),
                "EVENTRISKCALENDAR" => Some("economic-event information"),
                _ => None,
            };
            if let Some(name) = name {
                match status {
                    "STALE" => return format!("The {name} is out of date."),
                    "MISSING" => return format!("The {name} is unavailable."),
                    _ => {}
                }
            }
        }
    }
    let text = match code {
        "MC.ASSESSMENT.UNAVAILABLE" => "Required market inputs are unavailable or out of date.",
        "MC.ASSESSMENT.CALENDAR.COVERAGE_UNCONFIRMED" => {
            "Economic-event calendar coverage has not been confirmed."
        }
        "MC.ASSESSMENT.REGIME.STALE" => "The upstream regime assessment has expired.",
        "MC.ASSESSMENT.INHERITED_NO_NEW_TRADE" | "TS.REGIME.RESTRICTION" => {
            "Regime Discovery blocked new trades."
        }
        "RD.VOL.BACKWARDATION" => "VX futures were in backwardation.",
        "RD.VOL.EXTREME" => "Volatility was extreme.",
        "RD.VOL.HIGH" => "Volatility was high.",
        "TS.NO_AUTHORIZED_CANDIDATE" => "No trading strategy is authorized for this Fund.",
        "TS.NO_COMPATIBLE_CANDIDATE" => {
            "No configured strategy matches the current market conditions."
        }
        "TS.SELECTED" => "An eligible trading strategy was selected.",
        "TS.ASSESSMENT.SESSION" => {
            "The trading session does not meet this strategy's requirements."
        }
        "TS.ASSESSMENT.LIQUIDITY" => "Market liquidity does not meet this strategy's requirements.",
        "TS.ASSESSMENT.STRESS" => "Market stress exceeds this strategy's limits.",
        "TS.ASSESSMENT.EVENT" => "Economic-event risk does not meet this strategy's requirements.",
        "TS.ASSESSMENT.DATA_QUALITY" => {
            "Required market data does not meet the strategy's quality requirements."
        }
        "TS.ASSESSMENT.CONFIDENCE" | "TS.REGIME.CONFIDENCE" => {
            "Assessment confidence is below the strategy's minimum."
        }
        "TS.ASSESSMENT.TRIGGER" => {
            "The trading signal does not meet the strategy's trigger requirements."
        }
        "TS.ASSESSMENT.CONDITION" | "TS.VARIANT.CONDITION" => {
            "Market conditions do not match the strategy's entry rules."
        }
        "TS.ASSESSMENT.VOLATILITY"
        | "TS.VARIANT.VOLATILITY"
        | "TS.REGIME.VOLATILITY_LEVEL"
        | "TS.REGIME.VOLATILITY_CHANGE" => "Volatility does not match the strategy's entry rules.",
        "TS.PERMISSION.OPERATING_STATE" => "The Fund is not enabled for new trades.",
        "TS.PERMISSION.DIRECTION" => "The Fund does not permit this trade direction.",
        "TS.PERMISSION.PRODUCT" => "The Fund does not permit this product.",
        "TS.PERMISSION.CONDITION" => "The Fund does not permit trading in these market conditions.",
        "TS.PERMISSION.DEPLOYMENT" | "TS.PERMISSION.ENVELOPE" => {
            "The Fund's permissions do not authorize this strategy."
        }
        "TS.REGIME.DIRECTION" | "TS.VARIANT.DIRECTION" => {
            "Market direction does not match the strategy's entry rules."
        }
        "TS.REGIME.PHASE" | "TS.VARIANT.PHASE" => {
            "The trend phase does not match the strategy's entry rules."
        }
        "TS.REGIME.STRENGTH" | "TS.VARIANT.STRENGTH" => {
            "Trend strength does not meet the strategy's requirements."
        }
        "TS.REGIME.STRUCTURE" | "TS.VARIANT.STRUCTURE" => {
            "Market structure does not match the strategy's entry rules."
        }
        "TS.REGIME.QUALITY" => "Regime data quality does not meet the strategy's requirements.",
        "RM.PORTFOLIO.NO_TRADE_ORDERS" => "Portfolio approval produced no authorized trade orders.",
        "RM.PORTFOLIO.TRADE_ORDERS_AUTHORIZED" => "Portfolio approval authorized the trade orders.",
        _ => return readable_fallback(code),
    };
    text.into()
}

/// Preserves descriptive backend messages and gives unknown machine codes a readable fallback.
fn readable_fallback(code: &str) -> String {
    if code.contains(char::is_whitespace) {
        return brief(code);
    }
    let words = code
        .split('.')
        .skip(1)
        .collect::<Vec<_>>()
        .join(" ")
        .replace('_', " ")
        .to_lowercase();
    if words.is_empty() {
        return format!("Recorded reason: {}.", brief(code));
    }
    let mut chars = words.chars();
    format!(
        "{}{}.",
        chars.next().unwrap().to_uppercase(),
        chars.as_str()
    )
}
