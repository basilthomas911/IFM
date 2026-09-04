# Frontend Display-Only Policy

Status: **Normative UI architecture rule**

This policy applies to every IFM desktop or web frontend, including WinForms and the planned WPF UI.

## Backend authority

Backend query results and notifications are authoritative. After a subscribed message has been successfully transported and deserialized, the frontend must admit it to presentation processing. The frontend must not silently reject it because a domain field is zero, default, unexpected, incomplete, out of range, or inconsistent with rules known to the UI.

Business validation, data-quality validation, state-transition validation, and contract normalization belong to the backend that owns the data. If backend data is invalid, the backend must reject or correct it before publication, or publish an explicit error/availability state. The UI may expose that condition but must not invent a replacement value or hide the record.

In particular, fields that are not displayed, such as a persistence sequence identifier, must not be prerequisites for displaying an otherwise successfully delivered record. A field may be used for ordering or exact duplicate suppression only when the backend contract guarantees that field for every query and notification path.

## Permitted frontend logic

The frontend may:

- map backend records to labels, colors, icons, chart points, and formatted values;
- sort, group, paginate, and bound display history;
- select the data belonging to the operator's current screen context, such as the selected contract or timeframe;
- suppress an exact duplicate using a backend-guaranteed identity;
- coalesce replaceable telemetry when the screen contract explicitly defines latest-value display semantics; and
- display explicit unavailable, unknown, stale, or error states supplied by the backend.

Screen selection is presentation routing, not domain validation. Data outside the current selection can remain undisplayed, but a record inside the subscribed screen context must not be discarded because the UI disagrees with its contents.

## Prohibited frontend logic

The frontend must not:

- decide whether a backend signal, trade, risk result, or market state is valid;
- reject a record based on a hidden field or locally duplicated backend rule;
- recalculate or reinterpret an authoritative backend result;
- silently drop an unexpected record; or
- make a trading or business decision.

Unexpected data must remain observable through the display or an explicit diagnostic/error state so the owning backend can be corrected.

## Testing requirement

Presentation tests must use production-shaped payloads, including zero or default values in non-display metadata, and prove that those values do not prevent rendering. Tests for filtering may cover explicit operator selection and screen scope, but must not encode backend validity rules in the frontend.

## Futures ITI implementation note

The backend currently publishes the live ITI payload before its persistence sequence is reflected in that payload, while a later query returns the persisted positive sequence. The Strategy presentation path therefore admits zero-sequence notifications and excludes the persistence sequence from its display-event identity. Reconciliation reloads authoritative history and merges it without duplicating the live observation.
