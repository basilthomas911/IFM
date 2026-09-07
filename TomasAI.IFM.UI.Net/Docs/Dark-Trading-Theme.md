# Dark Trading Theme

> **Strategy catalog direction (2026-09-06):** The current Reference trade-family editor still manages existing product/timeframe family rows. A future Configuration editor will manage reusable strategies, structures, variants and deployments under the same Dark Trading Theme; Portfolio remains the Fund-assignment UI. No new catalog editor is implemented by this design update. See [ConfigurationDb strategy catalog design](../../TomasAI.IFM.Application.Storage/Docs/ConfigurationDb-Strategy-Catalog-Design-v1.0.md).

## Scope and default

Dark Trading Theme is the default appearance for all IFM Windows Forms screens, dialogs, embedded views and application-owned controls. It includes the main dashboard, Portfolio/Fund, Reference Data, Market Data, trading, Strategy Observation, administration and system-information screens.

The Portfolio screen's three equal sections and two-row Fund metrics strip are described in [Portfolio Administration layout and metrics](Portfolio-Administration-Layout-and-Metrics.md).

New windows **must inherit `DarkTradingForm`** and embedded views **must inherit `DarkTradingView`**, both in `TomasAI.IFM.UI.Net.Views.Presentation`. Their base classes apply the theme before display and to controls added later. `WinFormsViewNavigator` also applies the theme when resolving a view. The application sets the default font before constructing its first window. The UI architecture test rejects forms/views in the Views assembly that bypass these bases.

## Dashboard navigation availability

Trade Orders, Market Data, Portfolio, Fund, Reference Data and System Administration remain enabled during and outside trading hours, including when Databento is starting, recovering, failed or stopped. Databento core readiness does not restrict access to these screens. Enabled navigation captions remain white under the theme.

Feed health indicators continue to show backend lifecycle health. The feed start/stop action still requires a value date, an open market session or an already active feed, and no feed transition in progress. Existing order-action validation remains in the trading workflow; opening its screen does not submit an order.

## Design tokens

The authoritative constants are in [`DarkTradingTheme.cs`](../../TomasAI.IFM.UI.Net.Views/Presentation/DarkTradingTheme.cs).

| Token | Standard |
| --- | --- |
| Font family | Microsoft Sans Serif |
| Font size | 10 points; `GraphicsUnit.Point` |
| Normal font style | Regular |
| Emphasis | Bold at the same 10-point size; headings, selected tabs and existing Change-mode editable-field emphasis |
| Content background | Black, `#000000` / RGB(0, 0, 0) |
| Normal foreground | White, `#FFFFFF` / RGB(255, 255, 255) |
| Button/header/active-tab background | `#2D2D30` / RGB(45, 45, 48) |
| Hover background | `#3E3E40` / RGB(62, 62, 64) |
| Pressed background | `#1F1F20` / RGB(31, 31, 32) |
| Border and separator | Gray, `#808080` / RGB(128, 128, 128) |
| Disabled text | Gray, `#808080` |
| Selected item background | Windows selection blue, `SystemColors.Highlight`; use the current system value rather than hardcoding an assumed RGB value |
| Selected item foreground | White, `#FFFFFF` |
| Inactive tab text | Light gray, `#D3D3D3`; enabled navigation buttons remain white |
| Outer form frame | 3 pixels at the application baseline; reuse an existing frame instead of adding a second one |
| Control border | 1 pixel |
| Compact command spacing | 4 pixels between adjacent command buttons; 8 pixels separates command groups |
| Standard new content inset | 10 pixels, subject to the screen's established responsive layout |

## Control standards

| Control | Appearance and interaction |
| --- | --- |
| Forms, views, layout panels, group boxes, tab-page bodies | Black content with white captions. Forms have the gray outer frame; embedded views do not add another frame. |
| Labels | White; preserve explicit semantic/status colors. Label alignment follows the associated input's text baseline; multiline descriptions align to the first line. |
| LinkLabel | White link/visited text, blue active link, gray disabled link; preserve native link behavior. |
| TextBox, RichTextBox | Black background and white normal text, including read-only fields. Existing multiline, scrolling, selection, validation and editability remain controlled by the view. Native text selection uses the Windows highlight. |
| NumericUpDown | Black entry field, white normal text, 10-point font. Native spin-button behavior is retained. |
| ComboBox | Black field and dropdown items, white text, blue current/selected item. Shared owner drawing centers item text vertically. Disabled items use gray text on black. Native dropdown arrows retain their Windows rendering. |
| DateTimePicker | Use `DarkDateTimePicker`. Black painted date field, white text, gray border, gray disabled text; black calendar popup with white dates, white title, gray adjacent-month dates and native date selection. Keyboard editing and the accessible Value provider remain available. |
| MonthCalendar | Black content, white date/title text, gray adjacent-month dates. Native selection, navigation and accessibility remain in effect. |
| ListBox and CheckedListBox | Black background, white normal text and native blue selection. Checkbox behavior is retained. |
| ListView | Black rows, white normal text, blue selected rows. Details headers use the dark-gray surface, white text and gray separators. Existing per-item semantic colors are retained. |
| DataGridView | Black data cells, white normal text, blue selected cells, gray gridlines, dark-gray headers. Disable header visual styles so Windows cannot repaint headers light. Apply the same defaults to columns added during binding. Explicit semantic cell colors remain meaningful. |
| TreeView | Black background, white text; native selection, expand/collapse and keyboard behavior. |
| PropertyGrid | Black property/help surfaces, white text, subdued separators and blue focused selection. |
| Buttons | Flat dark-gray background, **white caption when enabled (`#FFFFFF`) and gray caption when disabled (`#808080`)**, gray border, shared hover and pressed colors. The theme updates ForeColor when the button or its parent changes enabled state and corrects later screen-specific color overrides. Disabled text-only captions are also painted gray for native-rendering contrast. Preserve commands, enabled states, default/cancel roles, images and keyboard behavior. |
| CheckBox and RadioButton | Black surroundings, white captions and native checked/focus indications. Colored status backgrounds remain intact. |
| Tabs | Use `DarkTabControl`; black chrome, dark-gray active tab, white bold selected caption, light-gray inactive captions and gray active border. Existing close-tab behavior remains available. |
| Menus, context menus, toolbars, status bars | Shared dark renderer and dark hover/pressed surfaces. Command/button captions follow the same white-enabled/gray-disabled rule, including navigation, feed commands, nested dropdowns and items added later. Separate health/status indicators retain their semantic colors. |
| Charts | Black plotting area, white labels/titles/legends, 10-point typography, gray axes and subdued dark-gray gridlines. Preserve series colors, data, scales and financial meaning. |
| Split containers and separators | One-pixel gray divider within the existing black drag area, with black child content. Preserve resizing behavior and minimum panel sizes. |
| Progress bars, track bars, scrollbars | Preserve native behavior, accessibility and meaningful progress/indicator colors; Windows may render native glyphs and chrome. |
| Tooltips, Windows message boxes, file dialogs and window title bars | Operating-system rendering. The theme does not replace system dialogs or their interaction contracts. |

## Semantic colors and edit state

The theme normalizes neutral surfaces. It must not reinterpret domain status, P/L, feed health, warnings, validation, trade-plan alerts or chart-series colors. A yellow status label with black text must remain yellow with black text. Command/button captions always use their enabled-state colors; the adjacent health indicator carries the feed-health colors. A read-only field is distinct from a disabled control: keep read-only data readable in white, while disabled input text is subdued where supported by the native control.

Changing a font after attachment preserves its style while normalizing its family and size. Existing Reference Data Change-mode bold emphasis is preserved. Theme application does not enable controls, save data, change selections or alter command handlers.

## Creating a new screen

```csharp
using TomasAI.IFM.UI.Net.Views.Presentation;
using TomasAI.IFM.UI.Net.Views.Trade.IronCondor;

public sealed class ExampleForm : DarkTradingForm
{
    public ExampleForm()
    {
        Text = "Example";
        var body = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10) };
        body.Controls.Add(new Label { Text = "Value date", AutoSize = true });
        body.Controls.Add(new DarkDateTimePicker());
        body.Controls.Add(new Button { Text = "Close", DialogResult = DialogResult.Cancel });
        Controls.Add(body);
    }
}
```

Use `DarkTradingView` in the same way for a reusable embedded editor. Normal controls added to its layout panels inherit the theme automatically. Use `DarkDateTimePicker` and `DarkTabControl` for native surfaces that ignore ordinary background properties. For unattached preview controls or an external host, call `DarkTradingTheme.Apply(control)` before measuring or showing them.

Avoid screen-specific color/font helpers. Existing Trade Orders, Market Data and dashboard typography/palette entry points delegate to the common implementation for compatibility. Portfolio factories use the shared tokens.

## Layout and first display

Apply the final 10-point font before calculating sizes or revealing a view. Normalize fonts on dynamically added controls and later style changes so controls do not shrink after reveal. Preserve the existing responsive layout and button groups; a color change must not silently reorder fields or actions.

The Trade Orders blotter's order-entry row uses a proportional table layout so Asset Price remains visible after font normalization and host resizing.

The common form/view bases enable optimized double buffering for their managed surfaces. Native child windows require their own rendering support. Existing trade-blotter loading covers and completion checks remain responsible for revealing fully populated data; buffering alone does not make asynchronous loading atomic. Do not apply `WS_EX_COMPOSITED` indiscriminately to all screens.

## Verification

- `DarkTradingThemeTests` enforces themed inheritance for every application form/view, late-control styling, semantic-color preservation, blue combo selection, dynamic grid/menu defaults and representative screen rendering.
- Existing dashboard, Portfolio, Reference Data, trade-family, grid/chart layout and blotter tests cover interaction and layout regressions.
- `IFM_DARK_THEME_RENDER_DIR` writes representative form/view PNGs; `IFM_BLOTTER_RENDER_DIR` writes the populated Trade Orders blotter render.
- Native control chrome and operating-system dialogs are the explicit rendering exceptions above. Do not describe them as custom-painted dark controls.

Run the theme and affected UI suites after adding a new screen or changing shared rendering. Inspect rendered controls at the supported display scale, especially dropdowns, disabled captions, calendar fields, headers and asynchronously loaded views.

For predefined multiple selections, use [CheckedDropdown](Checked-Dropdown-Control.md): a read-only summary with a searchable checked list. It follows the same 10pt font, black/white palette and blue selection, and retains unavailable saved values visibly until removed.

The native ListView header adapter uses [LVM_GETHEADER](https://learn.microsoft.com/en-us/windows/win32/controls/lvm-getheader) and paints the unused header area for both screen painting and [WM_PRINTCLIENT](https://learn.microsoft.com/en-us/windows/win32/gdi/wm-printclient) rendering, preserving the operator's column widths.

### Initial migration verification — 2026-09-06

Result: **84 passed, 0 failed, 0 skipped**. Both the Views library and UI application builds completed with **0 warnings and 0 errors**. Rendered checks confirmed the dark header remainder, readable disabled captions and the fully visible Asset Price field in the populated blotter.

The affected UI suites cover 84 cases, including the theme architecture/default-control checks and existing dashboard, Portfolio, Reference Data, trade-family, Operations, Market Outlook and first-display blotter checks. Representative renders cover Reference Data, Market Data, Portfolio, Fund Transactions, System Administration, New Order, Operations, Market Outlook and the populated Trade Orders blotter. The harness creates actual WinForms controls at the test environment's 96-DPI scale with mocked backend dependencies; this is not a live broker or market-data acceptance test.

Rendering hosts use a transparent window at valid screen coordinates. Native SplitContainer painting fails in this environment when its test host is placed entirely outside the screen. This test-host constraint does not require an application rendering workaround.

### Button caption verification — 2026-09-06

**52 UI tests passed** after removing Market Data's inverted caption-color overrides and standardizing navigation/feed command captions. Checks cover initial state, enable/disable transitions, disabled parent panels/toolbars, late color overrides, representative forms and rendered regular/toolbar button captions. Both ForeColor and the rendered caption follow white when enabled and gray (`#808080`) when disabled. The Views build completed with zero warnings and errors.
