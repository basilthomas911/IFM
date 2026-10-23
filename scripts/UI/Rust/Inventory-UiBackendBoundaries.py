"""Inventory working-tree UI sources and record the reviewed backend boundary findings.

Identifier paths are conservative dependency evidence, not a semantic call graph.
Run from any directory; does not start applications or modify application code.
"""
from __future__ import annotations

import collections
import csv
import importlib.util
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location("ui_inventory", Path(__file__).with_name("Update-RustUiModelInventory.py"))
inventory = importlib.util.module_from_spec(spec)
spec.loader.exec_module(inventory)
AREAS = ("TomasAI.IFM.UI.Net.ViewModels", "TomasAI.IFM.UI.Net.Models", "TomasAI.IFM.UI.Net.Services")
DEST = ROOT / "Documents/system/IFM-Rust-UI-Backend-Boundary-Inventory-2026-10-23.csv"

# Reviewed findings; individual methods and runtime branches are explained in the companion report.
OVERRIDES = {
    "SelectedOrderPriceRisk.cs": ("Backend prerequisite", "B1", "Bounded option payoff / maximum loss; active composer submission dependency."),
    "BrokerManualTradeOrderViewModel.cs": ("Split backend preparation from Rust orchestration", "B1;B2", "Move cash/commission totals, risk inputs, deployment selection, tick/price-band policy; retain drafts, confirmation and receipt reconciliation."),
    "FundMetricsViewModel.cs": ("Backend prerequisite", "B3", "Local ledger performance aggregation, Sharpe, drawdown, P&L and commission; called by Rust PortfolioSession."),
    "FuturesEodDataUIViewModel.cs": ("Rust presentation; backend market summary", "B6", "Maps server indicators; local Bollinger Position20-to-MDI percentage belongs in server summary under strict financial-transformation boundary. Colors remain local."),
    "FuturesTradeSignalUIViewModel.cs": ("Rust presentation; backend market summary", "B6", "Maps server signals; also derives MDI percentage and UpTrending/DownTrending/RangeBound label from Bollinger Position20 using local thresholds. Return classified summary from backend; keep colors/formatting local."),
    "IronCondorViewModel.cs": ("Split backend projection from Rust orchestration", "B4;B5;B7", "Current established path: shared-cache calculations, saved fill/position valuations, local session enablement. Legacy branch calculations are not standalone migration obligations."),
    "TradeMonitoringPolicy.cs": ("Backend session authority; Rust presentation", "B7", "CanStart derives session eligibility locally; CanLoad is a UI state predicate."),
    "PositionEntryWindow.cs": ("Backend session authority; Rust presentation", "B7", "Derives session/opening window through shared domain policy; use API eligibility for replay-controlled behavior."),
    "TradeOrderEditorViewModel.cs": ("Rust orchestration with backend eligibility", "B7", "Uses local TimeProvider/session policy to gate entry; other work is API orchestration and UI lifecycle."),
    "MarketDataFeedQueryService.cs": ("Rust API adapter with backend range query", "B7", "GetFuturesBarWindowAsync calculates exchange-session partitions including an 18:00 rule locally; move partition ownership to server."),
    "BrokerTradeInitializationQuery.cs": ("Rust API orchestration; backend evaluation clock", "B7", "Risk-envelope query currently uses DateTime.UtcNow; backend must own business evaluation instant."),
    "PortfolioAdministrationViewModel.cs": ("Rust API orchestration; backend evaluation clock", "B7", "Risk-envelope evaluation instant uses DateTime.UtcNow; local form validation can remain."),
    "PortfolioFundEditorModels.cs": ("Rust DTOs / drafts / UI hints", "C1", "Contract-reference parsing and closing-type/permission helpers are client convenience; server validates exact submitted contracts and operations. Prefer resolved contract IDs from API."),
    "FinancialControlPreparation.cs": ("Port to Rust", "C2", "Request construction, prechecks, expected versions and canonical hashes; no pricing engine or financial valuation."),
    "FinancialPostingDraft.cs": ("Port to Rust", "C2", "Operator drafts, amount/date checks and hashed requests; posting authority stays on existing backend."),
    "FinancialConfigurationOperation.cs": ("Port to Rust", "C2", "Pending operation identity, API submit, receipt correlation and uncertain-outcome handling."),
    "FinancialPostingOperation.cs": ("Port to Rust", "C2", "Pending operation identity, API submit, receipt correlation and uncertain-outcome handling."),
    "PendingFinancialOperationStore.cs": ("Port to Rust", "C2", "Client durable request journal compatibility; not backend financial calculation."),
    "PendingFinancialConfigurationStore.cs": ("Port to Rust", "C2", "Client durable request journal compatibility; not backend financial calculation."),
    "BrokerTradePreviewQuery.cs": ("Port existing API call to Rust", "C3", "Preview pricing/payoff already computed by backend; do not add a second preview engine."),
    "BrokerTradePreviewPresentation.cs": ("Rust formatting; backend summary preferred", "C3", "Formats backend preview; reserved-capital display sums Held+Working. Include this in server summary under strict no-financial-transformation boundary."),
    "BrokerTradeLegSelection.cs": ("Port to Rust", "C1", "Normalizes operator-selected buy/sell roles; server must validate strategy structure."),
    "BrokerOrderMutationRules.cs": ("Port to Rust", "C1", "UI enabled-state hints based on server statuses/bounds; server remains mutation authority."),
    "PortfolioTradeOrderService.cs": ("Port existing API orchestration to Rust", "C2", "Calls authoritative Portfolio evaluation and Trade lifecycle APIs; envelope hashing is protocol work, not pricing."),
    "OptionPricingService.cs": ("Exclude unused WinForms calculation path", "L1", "Runs local OptionCalculator.ImpliedVolatility; inspected caller is WinForms-only IronCondorTradeOrderViewModel.CalculateTradeValues."),
    "MarketDataQueryService.cs": ("Port API adapter to Rust; exclude old pricing preparation", "L1;C0", "Most methods are API adapters. GetRiskFreeRateAsync locally divides OneMonth yield by 100 for older Iron Condor paths; no new standalone pricing engine is required."),
    "IOptionPricingService.cs": ("Exclude unused WinForms calculation path", "L1", "Local pricing interface; catalog registration alone does not make its methods active in Rust."),
    "IronCondorTradeOrderViewModel.cs": ("Exclude unused WinForms entry path", "L1", "Current Rust TradeComposerSession uses BrokerManualTradeOrderViewModel, not this model; no new backend clone required."),
    "DesktopTradeExecutionContext.cs": ("Port to Rust", "C1", "Client route configuration and defensive checks; server authorizes actual account and broker environment."),
    "LookupTypeEditorViewModel.cs": ("Port to Rust", "C1", "GetNextOrderId calculates display ordering within a lookup type, not canonical trade/portfolio identity allocation."),
    "StatusConsoleViewModel.cs": ("Port to Rust", "C1", "BuildTradeStatus formats text from server trend/execute-state events; does not decide execution."),
}

def main():
    tracked = subprocess.check_output(["git", "ls-files", "--cached", "--others", "--exclude-standard"], cwd=ROOT).decode().splitlines()
    paths = sorted(p for p in tracked if p.endswith(".cs") and p.split("/")[0] in AREAS and (ROOT / p).is_file())
    texts = {p: inventory.read_source(ROOT / p) for p in paths}
    cleaned = {p: inventory.code(t) for p, t in texts.items()}
    symbols = collections.defaultdict(set)
    for p, t in cleaned.items():
        for name in inventory.DECLARATION.findall(t):
            symbols[name].add(p)
    edges = {p: set() for p in paths}
    roots = {}
    scan = paths + [p for p in tracked if p.startswith("TomasAI.IFM.UI.Rust.Bridge.Net/") and p.endswith(".cs") and (ROOT / p).is_file() and "/Fixture" not in p]
    for p in scan:
        t = cleaned.get(p) if p in cleaned else inventory.code(inventory.read_source(ROOT / p))
        for match in re.finditer(r"\b[A-Za-z_]\w*\b", t):
            for target in symbols.get(match.group(), ()):
                if target == p:
                    continue
                if p in edges:
                    edges[p].add(target)
                else:
                    roots.setdefault(target, f"{p}:{t.count(chr(10), 0, match.start()) + 1}")
    routes = {p: origin + " -> " + p for p, origin in roots.items()}
    pending = collections.deque(roots)
    while pending:
        source = pending.popleft()
        for target in sorted(edges[source]):
            if target not in routes:
                routes[target] = routes[source] + " -> " + target
                pending.append(target)
    existing = {}
    with (ROOT / "Documents/system/IFM-Windows-Rust-UI-Model-Inventory-2026-10-23.csv").open(encoding="utf-8-sig", newline="") as f:
        existing = {r["Source"]: r["Usage"] for r in csv.DictReader(f)}
    rows = []
    evidence = re.compile(r"Math\.|\.Sum\(|\.Average\(|Calculate|Compute|DateTime\.(?:UtcNow|Now)|FuturesTradingValueDate|FuturesMarketSessionPolicy|\s[*/]\s")
    for p in paths:
        decision, findings, note = OVERRIDES.get(Path(p).name, (
            "Port client behavior to Rust; no backend calculation identified", "C0",
            "DTO/state/presentation, form editing, notification, transport adapter or lifecycle work; preserve existing backend command/query authority. Source reference does not prove every method is reachable."))
        locations = [f"{p}:{i}: {line.strip()}" for i, line in enumerate(cleaned[p].splitlines(), 1) if evidence.search(line)]
        rows.append(dict(Source=p, Area=p.split("/")[0], Decision=decision, Findings=findings,
            SourceUsage=existing.get(p, "Service dependency; inspect caller / catalog"),
            ConservativeRustReferencePath=routes.get(p, "No identifier path found; not proof of dead code"),
            CalculationAndClockEvidence="; ".join(locations), Review=note,
            ImplementationStatus=("Moved active financial path to backend; runtime acceptance pending" if any(x in findings.split(";") for x in ("B1","B2","B3","B4","B5","B6","B7")) or Path(p).name == "BrokerTradePreviewPresentation.cs" else "Excluded WinForms path" if "L1" in findings else "Client responsibility"),
            ClosureEvidence="IFM-Current-Rust-UI-Backend-Processing-Implementation-Plan.md#implementation-and-verification-results"))
    with DEST.open("w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    print(f"Wrote {len(rows)} unique source rows to {DEST.name}")
    print(dict(collections.Counter(r["Area"] for r in rows)))
    assert len({r["Source"] for r in rows}) == len(paths)
    assert all((ROOT / r["Source"]).is_file() for r in rows)

if __name__ == "__main__":
    main()
