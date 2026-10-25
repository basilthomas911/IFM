"""Produce a conservative, source-linked inventory; never executes the application."""
from __future__ import annotations

import argparse
import bisect
import collections
import csv
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
AREAS = [
    "TomasAI.IFM.UI.Net.Views", "TomasAI.IFM.UI.Net.ViewModels",
    "TomasAI.IFM.UI.Net.Models", "TomasAI.IFM.UI.Net.Services",
    "TomasAI.IFM.UI.EventConsumer", "TomasAI.IFM.UI.Net.Composition",
    "TomasAI.IFM.Application.Api.Nats.Client", "TomasAI.IFM.UI.Rust.Bridge.Net",
    "TomasAI.IFM.UI.Rust/src", "TomasAI.IFM.UI.Net",
]
CANDIDATES = set(AREAS[1:3])
DECLARATION = re.compile(r"^\s*(?:(?:public|internal|private|protected|static|abstract|sealed|partial|readonly|ref|unsafe|file|new)\s+)*(?:record(?:\s+(?:class|struct))?|class|struct|interface|enum)\s+([A-Za-z_]\w*)", re.M)
# Preserve character offsets for source links. This is identifier-reference analysis,
# not a C# semantic model, method call graph, or reflection resolver.
NONCODE = re.compile(r'/\*.*?\*/|//[^\r\n]*|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"', re.S)


def code(text: str) -> str:
    return NONCODE.sub(lambda match: re.sub(r"[^\r\n]", " ", match.group()), text)


def read_source(path: Path) -> str:
    raw = path.read_bytes()
    if raw.startswith((b"\xff\xfe", b"\xfe\xff")):
        return raw.decode("utf-16")
    try:
        return raw.decode("utf-8-sig")
    except UnicodeDecodeError:
        return raw.decode("cp1252", errors="replace")


def owner(path: str) -> str:
    if "Tests/" in path or "Tests." in path or ".UnitTests/" in path:
        return "Test"
    if path.startswith("TomasAI.IFM.UI.Rust.Bridge.Net/Fixture"):
        return "Fixture"
    if path.startswith("TomasAI.IFM.UI.Rust.Bridge.Net/"):
        return "RustBridge"
    if path.startswith("TomasAI.IFM.UI.Net.Views/"):
        return "WinForms"
    if path.startswith(("TomasAI.IFM.UI.Net.Composition/", "TomasAI.IFM.UI.Net/Startup.cs")):
        return "Registration"
    if path.startswith("TomasAI.IFM.UI."):
        return "UiDependency"
    return "OtherProduction"


def disposition(path: str) -> str:
    name = Path(path).name
    if name == "GlobalUsings.cs":
        return "Replace C# imports with Rust module imports"
    if name == "IronCondorTradeOrderViewModel.cs":
        return "Exclude WinForms-only entry path from current Rust baseline; no new backend clone required"
    if name in {"FinancialControlPreparation.cs", "FinancialPostingDraft.cs"}:
        return "Port request construction, prechecks and canonical hashes to Rust; existing backend remains authoritative"
    if name in {"SelectedOrderPriceRisk.cs", "BrokerManualTradeOrderViewModel.cs", "FundMetricsViewModel.cs", "IronCondorViewModel.cs", "TradeMonitoringPolicy.cs", "PositionEntryWindow.cs", "FuturesEodDataUIViewModel.cs", "FuturesTradeSignalUIViewModel.cs"}:
        return "Split Rust presentation/drafts from backend-owned financial and eligibility rules"
    if name.startswith("PendingFinancial"):
        return "Port journal compatibility and preserve operation IDs, payloads, hashes and uncertain outcomes"
    if name == "EasternTime.cs":
        return "Port display-zone conversions only; ValueDate remains API-owned"
    if "/Contracts/" in path or name.startswith("IIFMApp"):
        return "Replace managed lifetime/interaction interface with Rust ownership and messages"
    return "Port presentation, client state, orchestration or formatting; retain server authority"


def dependency_disposition(path: str) -> str:
    if path.startswith("TomasAI.IFM.Application.Api.Nats.Client/"):
        return "Rust typed API adapter; preserve subjects, wire contracts, IDs and acknowledgement semantics"
    if path.startswith("TomasAI.IFM.UI.EventConsumer/"):
        return "Rust scoped subscription/fan-out; preserve filters, deduplication, error delivery and cleanup"
    if path.startswith("TomasAI.IFM.UI.Net.Composition/"):
        return "Replace managed registration with explicit Rust application composition"
    if "/OptionPricing/" in path:
        return "Exclude WinForms-only local pricing method from current Rust baseline; no new backend clone required"
    if path.startswith("TomasAI.IFM.UI.Net.Services/"):
        return "Port client orchestration; separate backend-owned business transformations"
    if Path(path).name in {"BridgeBootstrap.cs", "PooledBridgeBuffer.cs", "PublicationTransfer.cs", "BridgeWire.cs", "BridgeDocument.cs", "RustClientServiceScope.cs"}:
        return "Retire managed ABI/hosting/composition after equivalent Rust state/transport is qualified; review nested models"
    return "Port bridge-owned controller, publication/model, cache or action behavior to Rust; review embedded business rules"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--date", required=True, help="Inventory date, YYYY-MM-DD")
    args = parser.parse_args()
    tracked = subprocess.check_output(["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=ROOT).decode().split("\0")
    files = sorted(p for p in tracked if p.endswith(".cs") and (ROOT / p).is_file())
    texts = {p: code(read_source(ROOT / p)) for p in files}
    candidates = [p for p in files if p.split("/")[0] in CANDIDATES]
    symbols = {p: sorted(set(DECLARATION.findall(texts[p]))) for p in candidates}
    extensions = {p: sorted(set(re.findall(r"\b(\w+)(?:<[^>]+>)?\s*\(\s*this\s+", texts[p])))
                  for p in candidates if "/Extensions/" in p}
    symbol_files: dict[str, set[str]] = collections.defaultdict(set)
    for path, names in symbols.items():
        for name in names + extensions.get(path, []):
            symbol_files[name].add(path)
    references: dict[str, dict[str, list[str]]] = {p: collections.defaultdict(list) for p in candidates}
    for path, text in texts.items():
        matches: dict[str, int] = {}
        newlines = [match.start() for match in re.finditer("\n", text)]
        for match in re.finditer(r"\b[A-Za-z_]\w*\b", text):
            for target in symbol_files.get(match.group(), ()):
                if target != path and target not in matches:
                    matches[target] = bisect.bisect_left(newlines, match.start()) + 1
        for target, line in matches.items():
            references[target][owner(path)].append(f"{path}:{line}")
    rows = []
    categories = collections.Counter()
    for path in candidates:
        refs = references[path]
        if Path(path).name == "GlobalUsings.cs":
            usage = "C# import infrastructure; replace with Rust modules"
        elif refs["RustBridge"]:
            usage = "Rust bridge referenced"
        elif refs["WinForms"]:
            usage = "WinForms referenced; no direct Rust bridge reference"
        elif refs["UiDependency"] or refs["OtherProduction"]:
            usage = "Indirect production reference; entry path requires review"
        elif refs["Registration"]:
            usage = "Registration only; execution unverified"
        elif refs["Test"] or refs["Fixture"]:
            usage = "Test/fixture references only"
        else:
            usage = "No external identifier reference found"
        categories[usage] += 1
        rows.append(dict(Source=path, DeclaredTypes="; ".join(symbols[path]), ExtensionMethods="; ".join(extensions.get(path, [])), Usage=usage,
            RustBridgeReferences="; ".join(refs["RustBridge"]), WinFormsReferences="; ".join(refs["WinForms"]),
            UiDependencyReferences="; ".join(refs["UiDependency"]), RegistrationReferences="; ".join(refs["Registration"]),
            OtherProductionReferences="; ".join(refs["OtherProduction"]), TestReferences="; ".join(refs["Test"] + refs["Fixture"]),
            TargetDisposition=disposition(path)))
    destination = ROOT / "Documents/system"
    stem = f"IFM-Windows-Rust-UI-Model-Inventory-{args.date}"
    with (destination / f"{stem}.csv").open("w", encoding="utf-8", newline="") as output:
        writer = csv.DictWriter(output, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    supporting = []
    for path in files:
        if path.split("/")[0] in set(AREAS[3:8]):
            supporting.append(dict(Source=path, DeclaredTypes="; ".join(sorted(set(DECLARATION.findall(texts[path])))),
                TargetDisposition=dependency_disposition(path)))
    support_name = f"IFM-Windows-Rust-UI-Dependency-Inventory-{args.date}.csv"
    with (destination / support_name).open("w", encoding="utf-8", newline="") as output:
        writer = csv.DictWriter(output, fieldnames=["Source", "DeclaredTypes", "TargetDisposition"])
        writer.writeheader()
        writer.writerows(supporting)
    commit = subprocess.check_output(["git", "rev-parse", "--short", "HEAD"], cwd=ROOT).decode().strip()
    lines = [f"# Windows Rust UI model inventory — {args.date}", "",
        f"Source baseline: `{commit}`. Target: `TomasAI.IFM.UI.Rust.eGui`. This is a source/reference census; consult the native ownership ledger and full migration plan for implementation and acceptance status.", "",
        "## Method and limits", "",
        "Inventory includes every nonignored working-tree C# file in UI.Net.ViewModels and UI.Net.Models, including contracts, helpers, nested types and journals. "
        "Production and test C# files are scanned for type and extension-method identifier references, with comments and string literals excluded. "
        "Counts below are source files, not runtime screens or class counts. Build output is excluded; new untracked application sources are included. "
        "Reflection, dependency injection, overload resolution, generic resolution, same-name types and method-level reachability require manual reconciliation. "
        "A reference is evidence of a dependency, not proof that a user executed the path. A missing direct bridge reference does not establish dead code.", "",
        f"The [CSV]({stem}.csv) records all discovered production/test reference locations and a target disposition for each file. "
        "Rows without a proven entry path remain review items; none are silently excluded from migration.", "",
        f"The [supporting dependency CSV]({support_name}) inventories {len(supporting)} service, consumer, composition, API-client and bridge files, "
        "including models declared outside the two primary model projects. Backend/shared wire DTOs remain backend contracts; "
        "the implementation operation registry must close their transitive wire dependencies.", "",
        "The [backend boundary audit](IFM-Rust-UI-Backend-Boundary-Audit-2026-10-23.md) and its working-tree checklist refine these file-level dispositions with current Rust method/branch evidence. "
        "The current Rust UI is the standalone behavior baseline; unused WinForms paths do not require new backend equivalents.", "",
        "Regenerate from the repository root:", "", "```powershell",
        f"python scripts/UI/Rust/Update-RustUiModelInventory.py --date {args.date}", "```", "",
        "## Current source boundaries", "", "| Source area | Tracked source files |", "| --- | ---: |"]
    for area in AREAS:
        suffix = ".rs" if area.endswith("/src") else ".cs"
        count = sum(p.startswith(area + "/") and p.endswith(suffix) for p in tracked)
        lines.append(f"| `{area}` | {count} |")
    lines += ["", "## Model and view-model reference classification", "", "| Classification | Files |", "| --- | ---: |"]
    for label, count in sorted(categories.items()):
        lines.append(f"| {label} | {count} |")
    lines += [f"| **Total** | **{len(rows)}** |", "", "## Complete file checklist", "",
        "Each row requires a current-Rust port, backend split, or explicit inactive-path disposition. Current managed execution is not a completed standalone Rust port.", "",
        "| Source | Declared types | Reference evidence | Target disposition |", "| --- | --- | --- | --- |"]
    for row in rows:
        lines.append(f"| [{row['Source']}](../../{row['Source']}) | {row['DeclaredTypes'] or 'Imports only'} | {row['Usage']} | {row['TargetDisposition']} |")
    lines += ["", "## Supporting frontend dependency checklist", "",
        "Models and orchestration in these files cannot remain managed in the shipped application. "
        "This is a source checklist, not a claim that every declared method is reachable.", "",
        "| Source | Declared types | Target disposition |", "| --- | --- | --- |"]
    for row in supporting:
        lines.append(f"| [{row['Source']}](../../{row['Source']}) | {row['DeclaredTypes'] or 'Imports only'} | {row['TargetDisposition']} |")
    (destination / f"{stem}.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Inventoried {len(rows)} model/view-model files against {len(files)} working-tree C# files; baseline {commit}.")
    for category, count in sorted(categories.items()):
        print(f"{count}: {category}")


if __name__ == "__main__":
    main()
