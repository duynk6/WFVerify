# UI Automation & FlaUI Rules

1. **Threading Model:**
   - FlaUI / UIA3 COM objects must strictly be invoked on the dedicated STA thread managed by `UiThread`.
   - Never call `automation.GetDesktop()`, `element.FindAllDescendants()`, or pattern methods directly on thread pool threads.

2. **Selectors & Flakiness:**
   - Always use hierarchical selectors (`prefix:value > prefix:value`).
   - Use `Retry.WhileNull` when locating elements to prevent race conditions during UI transitions.
   - When a selector fails to find a matching control, calculate fuzzy Levenshtein distance on visible elements' `Name` and `AutomationId` and return the top 10 candidates in the error envelope.

3. **Modal Dialog Interception:**
   - Before performing interactions, check if a modal window is active.
   - If an unexpected modal dialog blocks the target element, immediately return `ErrorCode.BlockedByModal` along with the dialog's title, text, and button options.

4. **Interaction Fallbacks:**
   - Execute interactions using pattern fallback chains:
     - `Invoke`: InvokePattern -> SelectionItemPattern -> LegacyIAccessiblePattern -> Physical Click.
     - `SetValue`: ValuePattern -> Focus + SendKeys.
     - `Toggle`: TogglePattern -> SelectionItemPattern -> Invoke.
     - `Select`: SelectionItemPattern -> ExpandCollapse + find child + Select.
   - Always call `WaitWhileBusy(500ms)` after an interaction and verify results if `verify = true`.
