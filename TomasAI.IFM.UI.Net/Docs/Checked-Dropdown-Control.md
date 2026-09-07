# Checked dropdown control

`CheckedDropdown` is the shared Dark Trading Theme multi-selection control in `UI.Net.Views.Presentation`. Its read-only text box displays comma-separated labels, while `SelectedValues` retains canonical values. Operators cannot type or paste new codes into the summary.

The dropdown contains a search box and a checked list. Checking or unchecking changes the summary immediately without dismissing the list. Search matches labels and internal values and preserves selections outside the filter. Alt+Down/F4 opens the list, Down moves from search to the list, Space toggles a focused checkbox, and Escape or clicking outside closes the popup. The control owns and disposes its popup.

Use `SetItems(IEnumerable<CheckedDropdownItem>)` for predefined values and `SetSelectedValues` to load an existing model. Items have a stable value, display label and enabled flag. Missing or disabled selections are visibly marked Unavailable and can be unchecked; unavailable values cannot be newly checked. `SelectionChanged` reports checkbox changes, and `HasUnavailableSelections` identifies unresolved values.

Create Fund and Change Fund use this control for Underlyings, Asset Types, Directions and Market Conditions. The former CSV labels and editable CSV text boxes are removed. Underlyings come from the stored Databento Futures/FuturesOption indexes; the remaining groups come from ConfigurationDb. Source failures produce an explicit error and do not open a misleading empty editor. No default permissions are silently selected. Existing unavailable selections must be removed before saving.

The palette is black backgrounds, white enabled text, gray disabled command text, gray borders and native blue list selection, with Microsoft Sans Serif 10pt. Single-line labels share a vertical center with the controls.

See [ConfigurationDb lookup definitions](../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Lookup-Definitions.md).
