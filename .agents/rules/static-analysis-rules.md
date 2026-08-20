# Static Analysis & Roslyn Rules

1. **Partial Class Clustering:**
   - Analyzing a WinForms form requires loading all partial class files in the form's cluster (`Form1.cs`, `Form1.Designer.cs`, `Form1.*.cs`).
   - Construct a `CSharpCompilation` with WinForms and Drawing references. Tolerant compilation (allow errors if dependencies are missing, but extract syntactic & semantic models).

2. **Designer Model Parsing:**
   - Extract control definitions, hierarchy, and assigned properties (`Name`, `Text`, `Location`, `Size`, `TabIndex`, `Anchor`, `Dock`, `Visible`, `Enabled`, `AccessibleName`, `Font`, `AutoScaleMode`) from `InitializeComponent`.

3. **Rule Specifications:**
   - `WF001` (Error): Event handler wired (`+=`) but method does not exist.
   - `WF002` (Warning): Orphaned handler method matching `(object, EventArgs)` signature without event wiring.
   - `WF010` (Warning): Overlapping sibling controls in the same container.
   - `WF011` (Warning): Control bounds outside container `ClientSize`.
   - `WF012` (Info): Negative control `Location`.
   - `WF020` (Warning): Duplicate `TabIndex` among sibling controls.
   - `WF021` (Info): Non-sequential `TabIndex` or interactive control missing `TabIndex`.
   - `WF022` (Info): `TabIndex` does not match visual reading order (top-to-bottom, left-to-right).
   - `WF030` (Error): `Dock = Fill` combined with non-default `Anchor`.
   - `WF031` (Warning): Control in resizable container with `Anchor = Top, Left`.
   - `WF040` (Warning): Interactive control missing `AccessibleName` and `Text` is empty.
   - `WF041` (Info): Default control name (e.g., `button1`, `textBox3`).
   - `WF050` (Warning): Hardcoded font different from parent/form default font.
   - `WF051` (Info): `AutoScaleMode` not set to `Dpi` or `Font`.
   - `WF060` (Info): Control declared but not added to `Controls.Add`.
