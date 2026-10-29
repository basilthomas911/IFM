//! Source integrity and contract completeness guard for the unchanged current backend.

use sha2::{Digest, Sha256};
use std::path::Path;

#[test]
fn current_backend_source_and_build_inputs_remain_unchanged() {
    let baseline: serde_json::Value =
        serde_json::from_str(include_str!("../baseline/CurrentBackend.json")).unwrap();
    let repository = Path::new(env!("CARGO_MANIFEST_DIR")).parent().unwrap();
    for source in baseline["sourceFiles"].as_array().unwrap() {
        let relative = source["path"].as_str().unwrap();
        let text = std::fs::read_to_string(repository.join(relative)).unwrap();
        let normalized = text.trim_start_matches('\u{feff}').replace("\r\n", "\n");
        // Preserve the earlier user-requested published-history boundary fix.
        // Reverse only its exact reviewed fragments, then verify the original hash.
        let history: serde_json::Value = serde_json::from_str(include_str!(
            "../baseline/HistoricalAvailabilityCompatibility.json"
        ))
        .unwrap();
        let mut normalized = normalized;
        for patch in history
            .as_array()
            .unwrap()
            .iter()
            .filter(|p| p["path"] == relative)
        {
            let reviewed = patch["reviewed"].as_str().unwrap();
            assert_eq!(
                normalized.matches(reviewed).count(),
                1,
                "Historical availability fragment changed: {relative}"
            );
            normalized = normalized.replacen(reviewed, patch["original"].as_str().unwrap(), 1);
        }
        // The native lifecycle correction adds a cached ownership capability.
        // Strip only the exact reviewed additions; preserve all original hashes.
        let ownership = match relative {
            "TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs" => Some(
                "    /// <summary>Cached backend lifecycle authority. Native datasets supervise themselves;\n    /// callers must not start a second recovery episode or reset API actors for them.</summary>\n    MarketDataRecoveryOwnership DatasetRecoveryOwnership => MarketDataRecoveryOwnership.Process;\n",
            ),
            "TomasAI.IFM.Application.MarketData/Remote/MarketDataApiClient.cs" => Some(
                "    public MarketDataRecoveryOwnership DatasetRecoveryOwnership => readMirror.DatasetRecoveryOwnership;\n",
            ),
            "TomasAI.IFM.Application.MarketData/Remote/MarketDataReadMirror.State.cs" => Some(
                "    /// <summary>Ownership received with the backend snapshot, without a control RPC.</summary>\n    public MarketDataRecoveryOwnership DatasetRecoveryOwnership { get { lock (gate) return ownershipKnown ? status.RecoveryOwnership : MarketDataRecoveryOwnership.Unknown; } }\n",
            ),
            _ => None,
        };
        let normalized = if let Some(addition) = ownership {
            assert_eq!(
                normalized.matches(addition).count(),
                1,
                "Native ownership addition changed: {relative}"
            );
            normalized.replacen(addition, "", 1)
        } else {
            normalized
        };
        let startup: &[&str] = match relative {
            "TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs" => &[
                "    /// <summary>Local client has not received its backend ownership snapshot.</summary>\n    Unknown = -1,\n",
            ],
            "TomasAI.IFM.Application.MarketData/Remote/MarketDataReadMirror.State.cs" => &[
                "    bool ownershipKnown;\n",
                "            ownershipKnown = true;\n",
            ],
            _ => &[],
        };
        let mut normalized = normalized;
        for addition in startup {
            assert_eq!(
                normalized.matches(addition).count(),
                1,
                "Startup ownership addition changed: {relative}"
            );
            normalized = normalized.replacen(addition, "", 1);
        }
        // The recovery audit plan adds passive status diagnostics only. Reverse
        // that exact extension before checking the unchanged implementation hash.
        let normalized = if relative
            == "TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs"
        {
            let audit = include_str!("../baseline/RecoveryAuditAddition.txt").replace("\r\n", "\n");
            assert_eq!(
                normalized.matches(&audit).count(),
                1,
                "Recovery audit DTO addition changed"
            );
            normalized.replacen(&audit,"    [property: Key(8)] MarketDataRecoveryOwnership RecoveryOwnership = MarketDataRecoveryOwnership.Process);\n\n",1)
        } else {
            normalized
        };
        // User-approved additive contract fragments are the only input exceptions.
        // Reverse the exact reviewed additions, then still require the original
        // baseline hash. Any other edit in this contract remains a failure.
        let normalized = if relative
            == "TomasAI.IFM.Application.MarketData/Contracts/IMarketDataApi.Backend.cs"
        {
            let addition =
                include_str!("../baseline/RecoveryCapabilityAddition.txt").replace("\r\n", "\n");
            assert_eq!(
                normalized.matches(&addition).count(),
                1,
                "Approved capability addition changed"
            );
            let workload =
                include_str!("../baseline/NativeWorkloadAddition.txt").replace("\r\n", "\n");
            assert_eq!(
                normalized.matches(&workload).count(),
                1,
                "Approved native workload addition changed"
            );
            let normalized = normalized.replacen(&workload,
                "    [property: Key(6)] Databento.Workers.DatasetPublicationEnvelope[]? OptionObservations = null);\n", 1);
            normalized.replacen(
                &addition,
                "    [property: Key(7)] string[]? EnabledOptionTrades = null);\n",
                1,
            )
        } else {
            normalized
        };
        let actual = format!("{:x}", Sha256::digest(normalized.as_bytes()));
        // The shared stream receiver must accept queued executions behind an
        // ahead-of-stream query snapshot without regressing its latest cache.
        // Keep the original baseline intact and pin only these reviewed fixes;
        // every other source and all further edits still fail this guard.
        let reviewed = match relative {
            "TomasAI.IFM.Application.MarketData/DataBento/Workers/DatasetWorkerCurrentValues.cs" => {
                Some("f81331ab35dbbe1313d1cfa4127f3d3e745aa69bed14d8323188de4cf52487ad")
            }
            "TomasAI.IFM.Application.MarketData/Remote/MarketDataReadMirror.State.cs" => {
                Some("5477f522a124f7657368b7b4227603ec187ddc2c7837c329f43f99c592ecd1d8")
            }
            _ => None,
        };
        assert_eq!(
            actual,
            reviewed.unwrap_or_else(|| source["sha256Utf8Lf"].as_str().unwrap()),
            "Current backend changed: {relative}"
        );
    }
    assert_eq!(baseline["apiMethods"].as_array().unwrap().len(), 71);
    assert_eq!(baseline["apiProperties"].as_array().unwrap().len(), 2);
}
