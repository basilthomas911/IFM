"""Export source-file ownership, separately from method and desktop acceptance.

The old client intentionally remains intact. A managed file can therefore have
many old members which are not reachable from the current Rust application.
This report maps its supported responsibility, not every historical member.
The generated operation registry retains unresolved method-level rows verbatim.
"""
import csv
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
APP = ROOT / "TomasAI.IFM.UI.Rust.eGui"
DOCS = ROOT / "Documents/system"

GROUPS = {
    "shell": ["app/runtime.rs", "backend/configuration.rs", "navigation/shell.rs"],
    "lifecycle": ["app/runtime.rs", "state/publications.rs", "backend/transport.rs"],
    "protocol": ["backend/codec.rs", "backend/transport.rs", "backend/contracts.rs"],
    "notifications": ["services/notifications.rs", "state/sidebar_calendar.rs"],
    "market": ["services/outlook.rs", "services/market_history.rs", "services/strategy_stream.rs"],
    "strategy": ["state/strategy.rs", "state/strategy_inspection.rs", "services/strategy.rs"],
    "calendar": ["state/calendar.rs", "state/sidebar_calendar.rs"],
    "contracts": ["state/futures_contract.rs", "state/option_contract.rs", "state/provider_definitions.rs", "services/provider_mapping.rs"],
    "yield": ["state/yield_curve.rs"],
    "reference": ["state/lookup.rs", "state/strategy_catalog.rs", "services/reference.rs"],
    "parameters": ["state/parameters.rs", "services/parameters.rs", "services/parameter_fields.rs"],
    "actor-health": ["state/actor_health.rs"],
    "operations-health": ["state/operations_health.rs", "services/dashboard_health.rs"],
    "scheduler": ["state/scheduled_tasks.rs"],
    "backup": ["state/database_backup.rs", "services/database_backup.rs"],
    "portfolio": ["state/portfolio.rs", "state/portfolio/editors.rs", "state/portfolio/fund_editors.rs", "services/portfolio_commands.rs"],
    "financial": ["state/portfolio/financial_ui.rs", "services/financial.rs", "services/financial_preparation.rs", "services/financial_operations.rs"],
    "orders": ["state/trades.rs", "services/manual_orders.rs", "services/trade_orders.rs"],
    "composer": ["state/trades/composer.rs", "services/broker_initialization.rs", "services/option_chain.rs", "services/broker_preview.rs"],
    "submission": ["state/trades/submissions.rs", "services/order_preparation.rs", "services/order_submission.rs", "services/order_close.rs"],
    "monitor": ["state/trade_monitors/mod.rs", "state/monitor_cache.rs", "services/trade_monitor_queries.rs", "services/trade_feed_recovery.rs"],
    "eod": ["state/trades/eod.rs", "services/trade_eod.rs"],
    "clock": ["services/business_context.rs", "date_picker/mod.rs"],
}


def classify(source):
    name = Path(source).stem
    if name in {"IronCondorTradeOrderViewModel", "IronCondorTradeInfoViewModel", "OptionGreeksUiModel", "OptionPricingService", "IOptionPricingService"}:
        return "excluded", "composer", "WinForms-only entry/pricing branch; current Rust constructs BrokerManualTradeOrderViewModel. No local pricing port."
    if name in {"PortfolioConcurrencyRefreshModel", "PortfolioNavigationModel"}:
        return "test-only", "portfolio", "Inventory found test/fixture references only; not a production runtime dependency."
    if source.startswith("TomasAI.IFM.Application.Api.Nats.Client/"):
        return "method-registry", "protocol", "Per-method native ownership and positive call sites are in tests/fixtures/backend/operations.json; a whole API class is not declared migrated."
    if any(x in name for x in ("Financial", "FundMetrics")):
        return "merged", "financial", "Native request preparation/journals and backend-returned financial reports; preserve existing formula basis."
    if "Portfolio" in name or "FundEditor" in name:
        return "merged", "portfolio", "Native portfolio/fund controllers and typed backend commands."
    if "ActorHealth" in name or "SupervisorControl" in name:
        return "merged", "actor-health", "Native HTTP health reads and NATS supervisor controls."
    if "OperationsHealth" in name or "FeedHealthMonitor" in name:
        return "merged", "operations-health", "Native health adapter; unavailable observations cannot retain stale green."
    if "Scheduled" in name or name == "ScheduleTime":
        return "merged", "scheduler", "Native scheduler state, output paging, previews and commands; execution stays on server."
    if "Backup" in name:
        return "merged", "backup", "Native backup administration and scoped history/output; no local backup engine."
    if "EndOfDay" in name:
        return "merged", "eod", "Native dialog and existing server position transition."
    if any(x in name for x in ("TradeMonitor", "TradePlan", "TradePosition", "SpreadBar", "RealtimeClientMirror", "IronCondorViewModel", "TradeMonitoringPolicy", "StrategyPosition")):
        return "merged", "monitor", "Established monitor uses complete backend projections, range history and scoped live updates; old local calculation branches are excluded."
    if any(x in name for x in ("Submission", "Confirmation", "SelectedOrderPriceRisk")):
        return "merged", "submission", "Backend preparation/risk values with native confirmation and durable uncertain-operation reconciliation."
    if any(x in name for x in ("Broker", "OptionChain", "TradeComposer", "DesktopTradeExecutionContext")):
        return "merged", "composer", "Native composer/account/preview/execution services; backend owns pricing and financial amounts."
    if any(x in name for x in ("TradeOrder", "TradeQuery", "TradePlacement", "OrderExecution", "PlaceTrade", "FundOrderEditor", "TradeWorkspaceSession")):
        return "merged", "orders", "Native order workspace and scoped command/event owners; server is authoritative."
    if "Parameter" in name:
        return "merged", "parameters", "Native typed drafts, validation, lifecycle, assignment and inspection."
    if any(x in name for x in ("YieldCurve",)):
        return "merged", "yield", "Native yield editor with backend CRUD/import."
    if any(x in name for x in ("EconomicCalendar", "SidebarSession")):
        return "merged", "calendar", "Native calendar editor/sidebar and terminal notifications; API date authority."
    if any(x in name for x in ("Contract", "InstrumentDefinition", "Convention", "Provider")):
        return "merged", "contracts", "Native contract/provider drafts, paging and explicit Draft review."
    if any(x in name for x in ("Lookup", "Reference", "StrategyCatalog", "MDIForwardLossRatio", "BridgeCatalogData")):
        return "merged", "reference", "Native reference manager/catalog and backend queries."
    if any(x in name for x in ("Strategy", "FuturesIti", "MarketAssessment", "RiskObservation", "Inspection", "OperationsViewModel")):
        return "merged", "strategy", "Native strategy stream/history and inspection; values/decisions come from backend."
    if name in {"EasternTime", "PositionEntryWindow"}:
        return "replaced", "clock", "Display zone conversion remains local; business date/session eligibility comes only from API."
    if any(x in name for x in ("Market", "FuturesBar", "FuturesEod", "FuturesTrade", "FuturesRsi")):
        return "merged", "market", "Native API context/outlook/history/feed adapters and typed event consumption."
    if any(x in name for x in ("Notification", "EventCorrelation", "StatusConsole", "EventConsumer", "EventService")):
        return "merged", "notifications", "Typed native subscription owners replace managed observable/event wrappers; feature state handles scoped refresh."
    if name in {"IFMAppViewModel", "IIFMAppLiveViewAdapter", "SystemAdminViewModel", "BridgeBootstrap", "RustShellViewModelSession", "RustClientServiceScope", "DashboardSession", "UiClientServiceRegistration", "IUiServiceCatalog", "UiServiceCatalog", "ApplicationQueryService", "RustDiagnosticLogger"}:
        return "replaced", "shell", "Native startup/composition/navigation/configuration; no managed host or service catalog at runtime."
    infrastructure = {
        "BaseEditorViewModel", "IAppRoot", "IAsyncLifecycle", "IAsyncOperation", "IUiDispatcher", "IUserInteraction", "IViewNavigator",
        "ObservableUiServiceExtensions", "GlobalUsings", "AsyncLifecycleCoordinator", "AsyncOperation", "UiServiceOperationException",
        "ObservableObject", "PresentationColorRole", "PresentationError", "IUiService", "UiServiceBase", "ServiceResultMapping",
        "UiOperationError", "UiOperationException", "UiOperationResult", "IUiEventSubscription", "OwnedUiEventSubscription",
        "AsyncSessionOwner", "BridgeDocument", "BridgeInput", "BridgeWire", "CoalescedDisplayPublisher", "DisplayDescriptors",
        "EditorActionGateway", "FixtureViewModelSession", "PooledBridgeBuffer", "PublicationCache", "PublicationTransfer",
        "StringIds", "UiRequestHistory", "VirtualGridDataset",
    }
    if name in infrastructure:
        return "replaced", "lifecycle", "Managed observable/dispatcher/gateway/publication/interface infrastructure is replaced by native state, channels, task ownership and typed transport."
    raise ValueError(f"Source file needs an explicit ownership decision: {source}")


def main():
    rows = []
    for filename in ("IFM-Windows-Rust-UI-Model-Inventory-2026-10-23.csv", "IFM-Windows-Rust-UI-Dependency-Inventory-2026-10-23.csv"):
        for row in csv.DictReader((DOCS / filename).open(encoding="utf-8-sig")):
            source = row["Source"]
            disposition, group, reason = classify(source)
            owners = GROUPS[group]
            for owner in owners:
                if not (APP / "src" / owner).is_file():
                    raise ValueError(f"Missing native owner {owner}")
            rows.append({"source": source, "disposition": disposition, "group": group, "nativeOwners": owners, "basis": reason})
    baseline = json.loads((APP / "migration/screen-baseline.json").read_text())
    known = {row["source"] for row in rows}
    roots = {source.split("/")[0] for source in known}
    tracked = subprocess.check_output(["git", "ls-files", "--cached", "--others", "--exclude-standard"], cwd=ROOT, text=True).splitlines()
    current = {source for source in tracked if source.endswith(".cs") and source.split("/")[0] in roots}
    if current != known:
        raise ValueError(f"Source census changed; refresh and review the inventories. Added: {sorted(current-known)}; removed: {sorted(known-current)}")
    changed = [x["source"] for x in baseline["files"] if hashlib.sha256((ROOT / baseline["source"] / x["source"]).read_bytes()).hexdigest() != x["sha256"]]
    if changed:
        raise ValueError(f"Original hybrid source changed: {changed}")
    report = {"scope": "Current Rust UI responsibility mapping; historical API members retain separate reachability status. Not a desktop acceptance result.", "files": len(rows), "sourceCensusDrift": [], "originalHashesVerified": len(baseline["files"]), "rows": rows}
    (APP / "migration/native-ownership.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Mapped {len(rows)} source files; verified {len(baseline['files'])} unchanged original hashes.")


if __name__ == "__main__":
    main()
