"""Shared Rust documentation/spacing policy for handwritten and generated sources.

Requires tree-sitter==0.26.0 and tree-sitter-rust==0.24.2. Syntax nodes keep
comments out of string literals and distinguish declarations from initializers.
Existing declaration comments are retained. This module never changes Rust logic.
"""
from functools import lru_cache
from pathlib import Path
import json
import re
import subprocess
import textwrap
import threading

from tree_sitter import Language, Parser
import tree_sitter_rust

ROOT = Path(__file__).resolve().parents[1]
PARSER = Parser(Language(tree_sitter_rust.language()))
PARSER_LOCK = threading.Lock()
DECLARATIONS = {"function_item", "function_signature_item", "struct_item", "enum_item", "trait_item", "field_declaration"}
COMMENT_TYPES = {"line_comment", "block_comment"}


def parse(source):
    """Serialize parser access while allowing independent file formatting jobs."""
    with PARSER_LOCK:
        return PARSER.parse(source)


def words(name):
    """Expand Rust/C# identifiers without splitting familiar acronym runs."""
    name = name.removeprefix("r#")
    name = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1 \2", name)
    name = re.sub(r"([a-z0-9])([A-Z])", r"\1 \2", name)
    return name.replace("_", " ").strip()


def walk(node):
    """Yield each named syntax node in source order."""
    yield node
    for child in node.named_children:
        yield from walk(child)


def node_text(source, node):
    """Read the exact UTF-8 text covered by a syntax node."""
    return source[node.start_byte:node.end_byte].decode("utf-8") if node else ""


def attached(node, source):
    """Find declaration attributes/comments without claiming a prior item."""
    start = node.start_byte
    comments = []
    attributes = []
    previous = node.prev_named_sibling
    while previous and previous.type in COMMENT_TYPES | {"attribute_item"}:
        prefix = source[source.rfind(b"\n", 0, previous.start_byte) + 1:previous.start_byte]
        if prefix.strip():
            break
        gap = source[previous.end_byte:start]
        # A blank line separates an unrelated section comment from the item.
        if gap.count(b"\n") > 1:
            break
        if previous.type in COMMENT_TYPES:
            text = node_text(source, previous)
            if text.startswith("//!") or text.startswith("/*!"):
                break
            comments.append(text)
        else:
            attributes.append(node_text(source, previous))
        start = previous.start_byte
        previous = previous.prev_named_sibling
    return start, comments, attributes


def owner(node, source):
    """Identify the enclosing record, trait or implementation."""
    current = node.parent
    while current:
        if current.type in {"struct_item", "enum_item", "trait_item", "impl_item"}:
            name = current.child_by_field_name("name") or current.child_by_field_name("type")
            return node_text(source, name)
        current = current.parent
    return ""


@lru_cache(maxsize=1)
def contracts():
    """Index pinned backend members for useful generated field documentation."""
    rows = json.loads((ROOT / "tests/fixtures/backend/contracts.json").read_text(encoding="utf-8-sig"))
    result = {}
    for row in rows:
        result.setdefault(row["type"].rsplit(".", 1)[-1], []).append(row)
    return result


FUNCTIONS = {
    "from_backend": "Reads the pinned backend representation and validates its scalar values.",
    "serialize_backend": "Writes the backend representation using the declared wire layout.",
    "serialize": "Serializes this value through the supplied serializer.",
    "deserialize": "Deserializes this value while preserving the declared field and scalar rules.",
    "deserialize_backend": "Streams the positional backend record using its pinned field slots.",
    "serialize_value": "Serializes this value using the requested presentation mode.",
    "serialize_presentation": "Writes the presentation fields, applying the requested history omission policy.",
    "serialize_extras": "Appends the computed presentation fields without changing backend values.",
    "decode_presentation": "Decodes the immutable presentation record from its wire bytes.",
    "cell": "Resolves a named display cell; unsupported columns remain missing.",
    "scalar_cell": "Exposes this scalar as a typed display cell without a generic value tree.",
    "published_label": "Returns the published label while preserving unrecognized backend values.",
    "published_is_valid": "Checks that the published identity contains the required nonempty identifiers.",
    "expecting": "Describes the accepted input shape in deserialization errors.",
    "visit_i64": "Accepts a signed integer through this value's deserialization rules.",
    "visit_u64": "Accepts an unsigned integer through this value's deserialization rules.",
    "visit_str": "Accepts text through this value's deserialization rules.",
    "visit_string": "Accepts owned text through this value's deserialization rules.",
    "visit_seq": "Reads the sequence entries through this value's deserialization rules.",
    "visit_map": "Reads named entries through this value's deserialization rules.",
    "visit_none": "Handles an absent optional value during deserialization.",
    "visit_unit": "Handles an explicit null value during deserialization.",
    "visit_some": "Reads the supplied optional value through the nested deserializer.",
    "fmt": "Formats this value for the requested formatter.",
    "deref": "Borrows the underlying value without transferring ownership.",
    "as_ref": "Borrows the underlying representation without transferring ownership.",
    "clone": "Clones this value using its declared ownership semantics.",
    "eq": "Compares the values using this type's equality rules.",
    "drop": "Releases resources owned by this value when its lifetime ends.",
    "default": "Constructs the initial state used when no explicit value is supplied.",
    "from": "Converts the supplied value into this representation.",
    "from_env": "Loads configuration from the supported environment settings.",
    "upgrade": "Acquires shared payload ownership if the referenced publication is still alive.",
    "downgrade": "Creates a non-owning payload reference for reuse without extending its lifetime.",
    "ptr_eq": "Checks whether both wrappers share the same immutable payload allocation.",
    "wire_is_materialized": "Reports whether compatibility bytes have already been stored for this payload.",
    "label": "Returns the user-facing label for this value.",
    "text": "Returns the display text for the requested value or field.",
    "number": "Reads the numeric representation supported by the supplied value.",
    "string": "Reads the text representation expected by this component.",
    "len": "Returns the number of retained entries.",
    "is_empty": "Reports whether this value contains no entries.",
    "fields": "Appends named display fields to the destination map.",
    "fixture": "Builds the deterministic input used by this test or measurement.",
    "project": "Projects backend data into the UI's display representation.",
    "try_send": "Offers a message to the bounded channel and returns any delivery failure.",
    "prepare_publication": "Prepares the publication's immutable display records before painting.",
    "prepare_trade_display": "Prepares the selected trade's display records for reuse during painting.",
    "clear_trade_display": "Releases the selected trade's prepared display and chart state.",
    "invalidate_render_cache": "Invalidates prepared display data after an in-place publication change.",
    "history_view": "Builds the compact history view, omitting fields excluded by the wire policy.",
    "history_equivalent": "Compares only fields retained in the compact history representation.",
    "is_history_view": "Checks whether the fields omitted from history are already absent.",
}

FUNCTIONS.update({
    "raw": "Encodes a MessagePack value without adding a compression frame.",
    "checked_len": "Rejects message lengths that exceed the configured decompression bound.",
    "expand": "Expands a bounded compressed MessagePack payload.",
    "member": "Looks up the requested member in the supplied record.",
    "configured": "Reports whether an explicit configuration value is available.",
    "as_str": "Borrows the stored text without allocating a replacement string.",
    "as_i64": "Reads a signed integer when this value has the expected representation.",
    "as_u64": "Reads an unsigned integer when this value has the expected representation.",
    "as_bool": "Reads a boolean when this value has the expected representation.",
    "integer": "Reads the signed integer representation of the requested field.",
    "unsigned": "Reads the unsigned integer representation of the requested field.",
    "wire": "Returns the representation used at the backend wire boundary.",
    "field": "Resolves the requested field in the supplied record.",
    "path": "Follows the supplied field path through the record.",
    "items": "Exposes the collection's entries for typed traversal.",
    "concrete_kind": "Resolves the concrete contract kind for this value.",
    "collection_kind": "Resolves the element contract kind for this collection.",
    "field_kind": "Resolves a field's declared contract type from the compiled schema.",
    "enum_name": "Resolves a declared enum value to its published name.",
    "enum_names": "Lists the enum names declared in the compiled schema.",
    "enum_value": "Resolves a published enum name to its declared value.",
    "compiled": "Loads the schema registry embedded at build time.",
    "error_id": "Extracts the backend error identifier from the service result.",
    "service_result": "Validates the service-result envelope and exposes its payload or backend error.",
    "date_only_wire": "Encodes the supplied calendar date using the backend DateOnly representation.",
    "timestamp_wire": "Encodes the supplied timestamp using the backend wire representation.",
    "canonical_json": "Produces the canonical JSON text used when deriving stable identities.",
    "guid_from_hash": "Derives a stable GUID from the supplied canonical hash input.",
    "option_contract_id": "Builds the option identity from its exact backend contract components.",
    "default_timeout": "Returns the configured default budget for backend requests.",
    "watch_connection": "Subscribes to connection-state revisions without polling.",
    "reply_until": "Receives the request reply within the caller's deadline, retaining response headers.",
    "request_until_inner": "Performs the bounded request/reply exchange and decodes its service result.",
    "flush_until": "Waits for pending transport writes to flush within the supplied deadline.",
    "flush": "Flushes pending transport or buffered output before returning.",
    "get_typed_observation": "Loads and validates a typed HTTP health observation within the request budget.",
    "get_observation": "Loads an HTTP health observation and retains its response metadata.",
    "get_observation_inner": "Reads the bounded HTTP health response using the selected decoder.",
    "post_json_inner": "Sends the JSON request while enforcing HTTP status, response-size and deadline rules.",
    "last_day_of_month": "Finds the final valid day in the selected calendar month.",
    "month_name": "Returns the display caption for the requested calendar month.",
    "month_data": "Prepares calendar cells for the requested month.",
    "local_date": "Converts the supplied instant to a calendar date in the configured timezone.",
    "instant": "Reads a timestamp as an instant while retaining the supported precision.",
    "optional_instant": "Reads an optional timestamp without inventing a value for missing data.",
    "utc": "Reads the UTC timestamp representation expected by this component.",
    "occurred": "Reads the occurrence time used to order the supplied event.",
    "signal_time": "Extracts the workflow signal's published event time.",
    "signal_identity": "Extracts the workflow signal identity used for correlation and deduplication.",
    "log_path": "Resolves the diagnostic log location for this component.",
    "offer": "Offers a diagnostic record to the bounded writer queue without waiting for disk I/O.",
    "rejected": "Returns the count of diagnostic records rejected by the writer.",
    "shutdown": "Requests shutdown and waits for owned work to finish within its configured bounds.",
    "current_id": "Returns the identifier associated with the current diagnostic operation.",
    "measure_sync": "Records the elapsed time and outcome of the supplied synchronous operation.",
    "borrowed": "Exposes the borrowed display representation without cloning its owner.",
    "history_plan": "Creates a shared compact history-plan payload with lazy wire materialization.",
    "native_plan": "Returns the shared normalized native plan without encoding a wire round trip.",
    "bytes": "Borrows stored wire bytes, materializing a lazy compatibility representation when required.",
    "line": "Borrows the indexed line from the matching immutable output text.",
    "serialize_monitor": "Writes the compact monitor presentation while keeping backend financial values authoritative.",
    "present_forward_risk": "Projects published forward-risk values into the compatible display fields.",
    "forward_spread": "Returns the published forward spread used by the display.",
    "action_time": "Formats the published action timestamp for the trade display.",
    "documents": "Returns the display documents associated with the current accepted state.",
    "status_description": "Returns the explanatory text for the current published status.",
    "run_details": "Builds the selected scheduled run's detail fields.",
    "request_hash": "Calculates the canonical request hash used to identify persisted operations.",
    "stored_json": "Serializes the persisted operation using its canonical JSON representation.",
    "rehydrate": "Restores the typed operation state from its persisted representation.",
    "current_user_directory": "Resolves the current user's directory for local application state.",
    "directory": "Resolves the directory used by this component's local storage.",
    "receiver": "Returns the channel endpoint used to receive this component's updates.",
    "required_guid": "Validates a required nonempty GUID before it is used in a backend request.",
    "guid": "Reads and validates the GUID representation used by this component.",
    "typed_identity": "Builds the concrete backend identity from the supplied scope fields.",
    "unknown": "Constructs the explicit unknown state without treating it as a confirmed result.",
    "unavailable": "Constructs the unavailable result while retaining its failure reason.",
    "from_wire": "Converts the wire record to the component's typed representation.",
    "semantic_hash": "Hashes the canonical semantic content used to identify an operation.",
    "base64": "Encodes the supplied bytes using the expected Base64 alphabet.",
    "positive": "Checks the supplied value against the required positive-value constraint.",
    "expirations": "Loads the option expirations available for the current contract scope.",
    "may_be_active": "Reports whether the owned subscription may still be active and require cleanup.",
    "accept": "Accepts an incoming value only when this component's identity and revision checks pass.",
    "command_kind": "Resolves the declared command kind used for backend dispatch.",
    "semantic_candidate": "Builds the candidate content used for semantic operation comparison.",
    "normalized_instant": "Normalizes a published timestamp to its canonical instant representation.",
    "allowed_query": "Checks whether the requested query is permitted by the declared route policy.",
    "query_reply": "Builds the decoded reply for the requested backend query.",
    "equal_amount": "Compares monetary values without converting them through binary floating point.",
    "humanize": "Converts a declared identifier into a readable display caption.",
    "segments": "Splits the supplied path into the segments used for field traversal.",
    "timespan": "Formats the supplied duration for display.",
    "uuid_n": "Formats the UUID without separators for backend compatibility.",
    "raw_property": "Reads the original property without replacing its stored representation.",
    "typed_id": "Builds the backend's concrete identity from the current scope.",
    "typed_position": "Builds the backend's concrete position identity for the selected strategy.",
    "position_wire": "Builds the wire position identity expected by the backend contract.",
    "query_response": "Decodes and validates the response expected by this query.",
    "positions": "Iterates retained position snapshots in their prepared chronological order.",
    "current_plan": "Returns the accepted plan associated with the current position or selection.",
    "saved_leg": "Loads the saved position leg using its exact backend identity.",
    "eastern_midnight": "Resolves midnight in the market timezone, including daylight-saving rules.",
    "take_subscription": "Transfers the owned subscription so cleanup has one responsible owner.",
    "history_page": "Builds the current history page using its accepted backend paging state.",
    "history_key": "Builds the stable key used to deduplicate retained history snapshots.",
    "history_time": "Reads the published history time used for chronological ordering.",
    "remember": "Retains an accepted history snapshot while preserving replacement and ordering rules.",
    "saved": "Clones only the selected retained snapshot for the requested history index.",
    "replace_regime": "Replaces the selected regime while retaining the surrounding typed parameter state.",
    "legacy_page": "Builds the compatible page for legacy parameter versions.",
    "patch_fields": "Applies only the edited fields to the retained draft representation.",
    "selected_policy_wire": "Returns the selected policy's original backend representation.",
    "operation_store": "Opens the local operation store used for durable reconciliation.",
    "post_or_reconcile": "Submits a new posting or reconciles its existing persisted operation identity.",
    "cached_row": "Reuses the immutable row payload when its encoded content is unchanged.",
    "chart_row": "Reuses encoded chart data until the accepted chart revision changes.",
})

FIELDS = {
    "incarnation": "Unique owner lifetime used to reject results from an earlier open/close cycle.",
    "detail_generation": "Revision captured by detail reads so late results cannot replace newer selections.",
    "chart_generation": "Revision captured by chart reads so late results cannot replace a newer request.",
    "chart_version": "Content revision used to invalidate the encoded chart cache.",
    "row_payloads": "Weak references to reusable immutable row payloads; inactive publications are not retained.",
    "plan_rows": "Weak references and display stamps used to reuse unchanged compact plan payloads.",
    "chart_row": "Weak encoded-chart cache paired with the chart content revision.",
    "cleanup_unconfirmed": "Whether subscription cleanup still requires authoritative confirmation.",
    "last_feed_at": "Timestamp of the last accepted feed update.",
    "selected": "Current selection retained by this state or view.",
    "scope": "Identity and routing scope against which requests and incoming results are checked.",
    "context": "Context required to interpret the current operation or publication.",
    "history": "Retained historical data used by this component.",
    "realtime": "Current accepted realtime state, kept separate from historical snapshots.",
    "status": "Current status text or state exposed by this component.",
    "stopping": "Shared shutdown flag observed by the background runtime.",
    "wake": "Notification used to resume dispatch when channel state changes.",
    "nearest": "Prepared row index nearest to the published underlying price.",
    "strike_text": "Strike text formatted once for reuse while painting.",
    "call": "Index of the first matching call contract in the immutable publication.",
    "put": "Index of the first matching put contract in the immutable publication.",
    "spread_plot": "Prepared plot points retained until the trade, chart or live sample changes.",
}

TYPE_DESCRIPTIONS = {
    "Monitor": "Owns one trade monitor's authoritative snapshots, read generations and subscription state.",
    "Realtime": "Accepts realtime snapshots only when identity, composition and monitoring generation remain valid.",
    "History": "Retains accepted history snapshots and their stable chronological display order.",
    "OptionGrid": "Prepared strike rows and bounded cell text shared by paints of one immutable option chain.",
    "OptionStrikeRow": "Call/put contract indexes and strike caption for one displayed strike row.",
    "RowData": "Shared immutable publication payload with lazy typed display and compatibility-wire representations.",
    "WeakRowData": "Non-owning reference used to reuse a payload without retaining an inactive publication.",
    "Payload": "Storage alternatives for original wire bytes and compact native history-plan delivery.",
    "ComputePool": "Limits simultaneous CPU-heavy decode jobs independently of async request workers.",
    "WakeSender<T>": "Bounded channel sender that wakes native dispatch after sends and sender disposal.",
}

RECORD_FIELDS = {
    "OptionGrid": {
        "strikes": "Unique strike prices in descending display order.",
        "rows": "Prepared call/put indexes in the same order as `strikes`.",
        "text": "At most 1,024 formatted visible-contract entries, keyed by immutable contract index.",
    },
    "LineIndex": {"ranges": "UTF-8 byte ranges for each line; the original text remains owned by the publication."},
    "PreparedLines": {"text": "Owned immutable text used by the isolated line-index wrapper.", "index": "Line ranges prepared from exactly this owned text."},
    "PreparedMarket": {"value": "Authoritative market display record; no prices are recomputed here.", "grid": "Indexes and visible-cell text prepared from this market record."},
    "Payload": {"bytes": "Original MessagePack bytes retained as the compatibility source.", "decoded": "One lazily decoded, schema-checked presentation record shared by publication clones.", "raw": "Compact authoritative history-plan fields before display-only normalization.", "display": "Lazy normalized display copy, separate from the raw compatibility representation.", "wire": "Compatibility bytes materialized only when a serializer or inspector requests them."},
    "Monitor": {
        "trade": "Established trade definition that anchors this monitor's identity and original composition.",
        "trade_type": "Published strategy type used when selecting monitor and history representations.",
        "trade_action": "Published opening/closing action associated with the selected trade.",
        "fund_name": "Fund caption retained for monitor publications.",
        "selected": "Saved position selected for history display and optional detail hydration.",
        "observations": "Accepted option observations keyed by their contract identity.",
        "limits": "Published trade limits; this UI does not calculate replacements.",
        "balance": "Published balance retained as exact decimal text.",
        "live": "Whether live monitoring is enabled for this owner.",
        "collecting": "Whether the monitored feed is currently collecting updates.",
        "starting": "Whether live monitoring is awaiting its startup result.",
        "market_stopped": "Whether the market-data feed has reported a stopped state.",
        "chart_loading": "Whether a chart read is outstanding for the current chart generation.",
        "observations_loading": "Whether optional observation hydration is still outstanding.",
        "observations_error": "Most recent observation-hydration error exposed to the operator.",
        "plan_date": "Backend value date used when requesting plan history.",
        "page": "Accepted page of saved plan history, including its backend paging metadata.",
        "live_plans": "Live plan snapshots keyed by backend plan identity.",
        "chart": "Accepted chart series kept separate from live position and plan state.",
        "chart_error": "Most recent chart-read error exposed alongside retained chart data.",
        "plan_error": "Most recent plan-history error exposed alongside retained history.",
    },
}

TUPLE_FIELDS = {
    "RowData": "Shared payload ownership; cloning the wrapper does not clone its wire bytes.",
    "WeakRowData": "Weak payload ownership used by producer caches.",
    "ByteBuffer": "Owned backend bytes serialized as a binary buffer.",
    "ExactDecimal": "Validated exact decimal text retained without floating-point rounding.",
    "Direct": "Typed backend record read directly from the input sequence.",
    "Scalar": "Scalar decoded using the existing backend validation rules.",
    "OptionalRecords": "Optional bounded collection of directly decoded records.",
    "BackendDocument": "Borrowed backend record; the wrapper does not take ownership of the referenced record.",
    "WireValue": "Borrowed JSON value adapted at the existing compatibility boundary.",
    "AbortOnDrop": "Owned background task cancelled when this guard is dropped.",
    "Magnitude": "Fixed-width decimal digits used by exact comparison and tick arithmetic.",
}

SCOPED_FUNCTIONS = {
    ("src/app/runtime.rs", "run"): "Owns the native async runtime, dispatches UI intents and delivers bounded result batches until shutdown.",
    ("src/main.rs", "report_errors"): "Extracts worker failures that must remain visible through the UI's error reporting path.",
    ("src/backend/configuration.rs", "value"): "Reads the requested client setting while honoring explicit environment overrides.",
    ("src/backend/contracts.rs", "query"): "Builds a read-only actor subject and validates its routed identity.",
    ("src/backend/contracts.rs", "route"): "Builds the actor subject using the declared prefix, kind and safe entity identifier.",
    ("src/backend/contracts.rs", "name"): "Returns the pinned backend actor name used in NATS subjects.",
    ("src/backend/transport.rs", "request"): "Sends a request using the configured default budget; submitted writes are not automatically retried.",
    ("src/backend/transport.rs", "request_until"): "Sends the request within the caller's deadline and records transport or backend failures.",
    ("src/backend/date_display.rs", "short"): "Formats a backend calendar date using the supported display culture.",
    ("src/backend/date_display.rs", "GetDateFormatEx"): "Calls the Windows date formatter with the selected culture and calendar fields.",
    ("src/backend/date_display.rs", "GetTimeFormatEx"): "Calls the Windows time formatter with the selected culture and time fields.",
    ("src/backend/time_zone.rs", "EnumDynamicTimeZoneInformation"): "Reads a Windows timezone definition by its enumeration index.",
    ("src/backend/time_zone.rs", "SystemTimeToTzSpecificLocalTimeEx"): "Converts a UTC SYSTEMTIME using a Windows timezone's historical rules.",
    ("src/console_capacity.rs", "current"): "Returns the current console retention bound derived from its visible viewport.",
    ("src/state/trade_monitors/data.rs", "flag"): "Formats a monitor flag using the canonical True/False context text.",
    ("src/state/trade_monitors/data.rs", "has_chart"): "Reports whether this monitor already has an accepted chart series.",
    ("src/state/trade_monitors/data.rs", "set_chart"): "Stores the accepted chart and advances the revision used by its encoded payload cache.",
    ("src/state/trade_monitors/data.rs", "iron"): "Checks whether the established trade uses the Iron Condor strategy.",
}


def describe(node, source, relative):
    """Describe a declaration using its schema, role, signature and local context."""
    name = node_text(source, node.child_by_field_name("name"))
    parent = owner(node, source)
    domain = words(Path(relative).stem)
    _, _, attrs = attached(node, source)
    if node.type.startswith("function"):
        if any(re.search(r"#\[(?:tokio::)?test\b", a) for a in attrs):
            return "Verifies that " + words(name) + "."
        if (relative, name) in SCOPED_FUNCTIONS:
            return SCOPED_FUNCTIONS[relative, name]
        if name in FUNCTIONS:
            return FUNCTIONS[name]
        if name == "new":
            return f"Constructs {parent or 'the initial state'} from the supplied inputs."
        if name in {"draw", "draw_inside", "show", "ui"} or name.startswith("draw_"):
            subject = words(name.removeprefix("draw_") if name.startswith("draw_") else parent or domain)
            return f"Renders the {subject.lower()} view and collects its UI interactions."
        if name == "execute" and "/state/" in "/" + relative:
            return f"Handles a {domain} intent and returns the resulting publication or error."
        verbs = {"load": "Loads", "read": "Reads", "decode": "Decodes", "encode": "Encodes", "parse": "Parses", "validate": "Validates", "check": "Checks", "prepare": "Prepares", "refresh": "Refreshes", "clear": "Clears", "set": "Sets", "apply": "Applies", "append": "Appends", "remove": "Removes", "open": "Opens", "close": "Closes", "select": "Selects", "save": "Saves", "write": "Writes", "render": "Renders", "build": "Builds", "capture": "Captures", "merge": "Merges", "reconcile": "Reconciles", "submit": "Submits", "release": "Releases", "start": "Starts", "stop": "Stops", "cancel": "Cancels", "schedule": "Schedules", "publish": "Publishes", "retain": "Retains", "record": "Records", "restore": "Restores", "edit": "Edits", "choose": "Chooses", "update": "Updates", "collect": "Collects", "ensure": "Ensures", "find": "Finds", "trim": "Trims", "normalize": "Normalizes", "format": "Formats", "compare": "Compares", "register": "Registers", "install": "Installs", "connect": "Connects", "wait": "Waits for", "observe": "Observes", "poll": "Polls", "flatten": "Flattens", "resolve": "Resolves"}
        parts = words(name).split()
        if parts and parts[0] in verbs:
            subject = " ".join(parts[1:]) or domain
            return f"{verbs[parts[0]]} {subject} for {words(parent).lower() if parent else domain}."
        if name.startswith(("is_", "has_", "can_", "same_", "matches_", "accepts")):
            return f"Checks {words(name)} against the current {domain} state."
        if name.startswith("benchmark_") or relative.startswith("benches/"):
            return f"Measures {words(name.removeprefix('benchmark_'))} using the deterministic benchmark fixtures."
        if name == "main":
            return f"Starts the {domain if domain != 'main' else 'desktop application'} entry point."
        return f"Provides {words(name)} for the {words(parent).lower() if parent else domain} implementation."
    if node.type in {"struct_item", "enum_item", "trait_item"}:
        if name in TYPE_DESCRIPTIONS:
            return TYPE_DESCRIPTIONS[name]
        candidates = contracts().get(name, []) if relative.endswith("trade_contracts.rs") else []
        if len(candidates) == 1:
            return f"Typed backend contract for `{candidates[0]['type']}`; preserves its pinned wire layout."
        if node.type == "trait_item":
            return f"Defines the {words(name).lower()} operations shared by its implementations."
        if relative.startswith("tests/"):
            return f"{words(name)} used by the {domain} test fixtures."
        if name.endswith("Controller"):
            return f"Owns {domain} selection, local drafts and correlated backend operation state."
        if name.endswith("Settings"):
            return f"Configuration values used by the {domain} component."
        if name.endswith("State"):
            return f"State retained between {domain} updates."
        if name.endswith("Display"):
            return f"Typed fields presented by the {domain} view."
        if name.endswith("Row"):
            return f"Display values for one {words(name.removesuffix('Row')).lower()} row."
        return f"{words(name)} used by the {domain} component."
    field_type = node_text(source, node.child_by_field_name("type"))
    if relative == "src/models/actor_health.rs":
        return f"HTTP `{''.join(part[:1].upper() + part[1:] for part in name.split('_'))}` field; missing, null and supplied values remain distinct."
    if name in RECORD_FIELDS.get(parent, {}):
        return RECORD_FIELDS[parent][name]
    if relative == "src/backend/transport.rs" and parent == "Settings":
        return {"url": "Explicit NATS endpoint loaded from the selected client profile or environment.", "credentials_file": "Optional NATS credentials file; used when configured instead of token-only authentication.", "token": "Optional NATS authentication token loaded from configuration.", "request_timeout": "Overall request budget used to establish caller deadlines."}.get(name, words(name) + ".")
    candidates = contracts().get(parent, []) if relative.endswith("trade_contracts.rs") else []
    if len(candidates) == 1:
        for member in candidates[0]["members"]:
            if re.sub(r"[^a-z0-9]", "", member["name"].lower()) == name.removeprefix("r#").replace("_", "").lower():
                detail = f"Backend `{member['name']}` at positional slot {member['index']} (`{member['type']}`)."
                if "DecimalText" in field_type:
                    detail += " Preserves decimal text without binary floating-point conversion."
                return detail
    if name in FIELDS:
        return FIELDS[name]
    label = words(name)
    if field_type.startswith("Option<"):
        return f"Optional {label}; `None` represents an absent value."
    if field_type == "bool":
        return f"Whether {label.removeprefix('is ')} is enabled or currently true."
    if "Weak" in field_type:
        return f"Weak reference to {label}; does not extend the referenced value's lifetime."
    if "OnceLock" in field_type:
        return f"Lazily initialized {label}, retained for this value's lifetime."
    if "BTreeMap<" in field_type or "HashMap<" in field_type:
        return f"Keyed {label} retained by {parent or domain}."
    if field_type.startswith("Vec<"):
        return f"Ordered {label} retained by {parent or domain}."
    return f"{label[:1].upper() + label[1:]} associated with this {words(parent).lower() or domain}."


def annotate(source, relative):
    """Add missing declaration comments without replacing existing prose."""
    tree = parse(source)
    edits = []
    for node in walk(tree.root_node):
        if node.type == "ordered_field_declaration_list" and node.parent.type == "struct_item":
            parent = node_text(source, node.parent.child_by_field_name("name"))
            item_line = source.rfind(b"\n", 0, node.parent.start_byte) + 1
            indent = source[item_line:node.parent.start_byte] + b"    "
            siblings = node.named_children
            for index, field in enumerate(siblings):
                if field.type in COMMENT_TYPES | {"attribute_item", "visibility_modifier"}:
                    continue
                start, comments, _ = attached(field, source)
                if comments:
                    continue
                previous = siblings[index - 1] if index else None
                if previous and previous.type == "visibility_modifier":
                    start = previous.start_byte
                    _, comments, _ = attached(previous, source)
                    if comments:
                        continue
                description = TUPLE_FIELDS.get(parent, f"Wrapped `{node_text(source, field)}` value owned by {parent}.")
                edits.append((start, b"\n" + indent + b"/// " + description.encode("utf-8") + b"\n" + indent))
        if node.type not in DECLARATIONS:
            continue
        start, comments, _ = attached(node, source)
        if comments:
            continue
        line = source.rfind(b"\n", 0, start) + 1
        indent = source[line:start]
        if indent.strip():
            raise ValueError(f"Run rustfmt before annotating inline declarations: {relative}")
        description = describe(node, source, relative)
        lines = textwrap.wrap(description, width=max(40, 96 - len(indent)), break_long_words=False, break_on_hyphens=False)
        doc = b"\n".join(indent + b"/// " + text.encode("utf-8") for text in lines) + b"\n"
        edits.append((line, doc))
    for position, text in sorted(edits, reverse=True):
        source = source[:position] + text + source[position:]
    return source


def space_definitions(source):
    """Separate function/type declarations from preceding items with visible space."""
    tree = parse(source)
    edits = []
    for node in walk(tree.root_node):
        if node.type not in DECLARATIONS - {"field_declaration"}:
            continue
        start, _, _ = attached(node, source)
        line = source.rfind(b"\n", 0, start) + 1
        previous = line
        while previous > 0 and source[previous - 1:previous] in (b"\n", b"\r"):
            previous -= 1
        if previous == 0:
            continue
        # No padding immediately after an opening scope; declarations otherwise
        # receive two blank lines for functions and one for types/traits.
        preceding_line = source[:previous].rsplit(b"\n", 1)[-1].strip()
        if preceding_line.endswith(b"{"):
            continue
        desired = 3 if node.type.startswith("function") else 2
        existing = source[previous:line].count(b"\n")
        if existing < desired:
            edits.append((line, b"\n" * (desired - existing)))
    for position, text in sorted(edits, reverse=True):
        source = source[:position] + text + source[position:]
    return source


def rustfmt(source):
    """Apply stable Rust formatting without following modules or reordering items."""
    result = subprocess.run(["rustfmt", "--edition", "2024", "--emit", "stdout", "--config", "skip_children=true,reorder_imports=false,reorder_modules=false"], input=source, capture_output=True, check=True)
    return result.stdout.replace(b"\r\n", b"\n")


def format_source(source, relative, *, preformatted=False):
    """Apply canonical layout, missing documentation, then declaration spacing."""
    if isinstance(source, str):
        source = source.encode("utf-8")
    if not preformatted:
        source = rustfmt(source)
    source = annotate(source, relative)
    return space_definitions(rustfmt(source)).decode("utf-8")


def code_tokens(source):
    """Return non-comment tokens for a formatting-only semantic safety check."""
    tree = parse(source)
    result = []
    def visit(node):
        if node.type in COMMENT_TYPES:
            return
        if not node.children:
            if node.type == "," and node.parent.type == "ordered_field_declaration_list":
                return
            result.append((node.type, source[node.start_byte:node.end_byte]))
        else:
            for child in node.children:
                visit(child)
    visit(tree.root_node)
    return result
