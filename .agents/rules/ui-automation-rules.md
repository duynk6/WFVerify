# UI Automation & FlaUI Rules

1. **Threading Model:**
   - FlaUI / UIA3 COM objects must strictly be invoked on the dedicated STA thread managed by `UiThread`.
   - Never call `automation.GetDesktop()`, `element.FindAllDescendants()`, or pattern methods directly on thread pool threads.

1b. **Never read UIA properties through the FlaUI shortcuts.**
   - `element.AutomationId`, `.Name`, `.ClassName`, `.HelpText`, `.ControlType`, `.IsEnabled`, `.IsOffscreen`, `.BoundingRectangle` all resolve to `Properties.X.Value`, which **throws `PropertyNotSupportedException`** when the provider does not supply the property. WinForms `MenuStrip` / `ToolStrip` items do not supply `AutomationId [#30011]`, which crashed tree walking and element search on any form with a menu.
   - Use the `UiaSafe` extensions in `Infrastructure/UiaSafe.cs` (`SafeName()`, `SafeAutomationId()`, `SafeLabel()`, ...) — they go through `ValueOrDefault` and swallow COM errors.
   - When iterating many elements, also guard **per element** so one bad node cannot abort the whole listing.

2. **Selectors & Flakiness:**
   - Always use hierarchical selectors (`prefix:value > prefix:value`).
   - Use `Retry.WhileNull` when locating elements to prevent race conditions during UI transitions.
   - When a selector fails to find a matching control, calculate fuzzy Levenshtein distance on visible elements' `Name` and `AutomationId` and return the top 10 candidates in the error envelope.

3. **Modal Dialog Interception:**
   - Before performing interactions, check if a modal window is active.
   - If an unexpected modal dialog blocks the target element, immediately return `ErrorCode.BlockedByModal` along with the dialog's title, text, and button options.

4. **Interaction Fallbacks:**
   - Execute interactions using pattern fallback chains:
     - `Invoke`: **non-blocking input first** — `PostMessage(BM_CLICK)` if the element has an HWND, else a physical click at the bounding-rectangle centre — then InvokePattern -> SelectionItemPattern -> LegacyIAccessiblePattern -> TogglePattern.
       **Do NOT "fix" this to be pattern-first.** `InvokePattern.Invoke()` is synchronous: if the handler calls `MessageBox.Show()`, it does not return until the dialog is dismissed, so the STA thread hangs until the hard timeout fires and poison detection reports a broken session. Verified: reordering to pattern-first makes `LiveUiWorkflowTests` time out after 5s at the `btnLogin` click.
     - `SetValue`: ValuePattern -> Focus + SendKeys.
     - `Toggle`: TogglePattern -> SelectionItemPattern -> Invoke.
     - `Select`: SelectionItemPattern -> ExpandCollapse + find child + Select.
   - Always call `WaitWhileBusy(500ms)` after an interaction and verify results if `verify = true`.
