# WinFormsStubGen

`WinFormsStubGen` is a .NET 10 console tool built with Mono.Cecil (0.11.6) that reads Windows Desktop .NET 10 reference assemblies (`System.Windows.Forms.dll`, `System.Windows.Forms.Primitives.dll`, and `System.Drawing.Common.dll`) and synthesizes a headless, state-holding compatibility stub assembly named `LiveSplit.Compat.WinForms.dll`.

This enables unmodified Windows .NET Framework LiveSplit auto-splitter component DLLs (such as `LiveSplit.Celeste.dll`) to load and run on Linux/.NET 10. These components typically construct Windows Forms settings user controls (using `TableLayoutPanel`, `CheckBox`, `NumericUpDown`, `TextBox`, `ComboBox`, etc.) and read/write their state during timer operations.

---

## Command-Line Interface (CLI)

```bash
WinFormsStubGen --ref-dir <dir> --core <path> --drawing <path> --out <dir>
```

### Options

| Option | Description |
|---|---|
| `--ref-dir <dir>` | Directory containing Windows Desktop reference assemblies (`System.Windows.Forms.dll`, `System.Windows.Forms.Primitives.dll`, `System.Drawing.Common.dll`) from `Microsoft.WindowsDesktop.App.Ref`. |
| `--core <path>` | Path to built `LiveSplit.Core.dll`. |
| `--drawing <path>` | Path to built `LiveSplit.Drawing.dll`. |
| `--out <dir>` | Target output directory where `LiveSplit.Compat.WinForms.dll` and companion assemblies will be placed. |

---

## How It Works

### 1. Module Setup and Primitives Merging
- Loads `System.Windows.Forms.dll` reference assembly into Mono.Cecil.
- Extracts and clones public types from `System.Windows.Forms.Primitives.dll` directly into the target assembly using a two-phase cloning pass so self-referencing enums and types resolve locally.
- Sets output module name and assembly definition to `LiveSplit.Compat.WinForms`, while keeping original type namespaces (`System.Windows.Forms.*`).
- Strips the `[ReferenceAssembly]` attribute and ref-assembly markers, emitting a runnable `.dll`.

### 2. Type Retargeting and Core Deduplication
- Drops types that already exist as public types in `LiveSplit.Core.dll` (e.g. `Keys`, `DialogResult`, `MessageBox`, `MessageBoxButtons`, `IWin32Window`, etc.).
- Adds exported type forwarders (`[TypeForwardedTo]`) pointing to `LiveSplit.Core` so existing references resolve seamlessly.
- Retargets `System.Drawing.Common` references:
  - Framework primitives (`Color`, `Point`, `PointF`, `Size`, `SizeF`, `Rectangle`, `RectangleF`, etc.) retarget to `System.Drawing.Primitives`.
  - Types implemented by `LiveSplit.Drawing` (`Font`, `FontFamily`, `FontStyle`, `GraphicsUnit`, `StringFormat`, `Brush`, `Pen`, etc.) retarget to `LiveSplit.Drawing`.
  - Missing interfaces such as `System.Drawing.IDeviceContext` retarget to `LiveSplit.Compat.WinForms.Support`.
- Strips custom attributes that reference types from dropped or missing assemblies.
- Scans and neutralizes orphaned `TypeReference`s in Cecil's metadata tables, and purges references to missing assemblies (`System.Drawing.Common`, `System.Windows.Forms.Primitives`, `System.Private.Windows.Core`, etc.).

### 3. Instantiability & Constructors
- Removes `abstract` modifier from classes and methods.
- Generates parameterless constructors with IL bodies that chain to the best-matching base class constructor passing default/null arguments.
- Replaces dummy `throw null` constructor bodies with valid base constructor invocation chains.

### 4. Property Backing Fields & Lazy Collections
- Generates private backing fields (`<PropertyName>k__BackingField`) for all non-abstract, non-indexer properties so values round-trip across getters and setters.
- Synthesizes lazy initialization for collection and style properties (e.g., `Control.Controls`, `TableLayoutPanel.ColumnStyles`, `TableLayoutPanel.RowStyles`, `ComboBox.Items`, `ListBox.Items`, `Control.DataBindings`, etc.).
- Tracks collection ownership via `StubSupport.SetCollectionOwner` so `Controls.Add(child)` immediately wires `child.Parent = container`.
- Clamps values in `NumericUpDown.Value` between `Minimum` (default 0) and `Maximum` (default 100).

### 5. Collection and Event Semantics
- Collection methods (`Add`, `AddRange`, `Count`, `Clear`, `Remove`, `Insert`, `IndexOf`, indexer `this[int]`, `IEnumerable`) operate in-memory via `StubSupport` helper routines.
- Event `add` and `remove` accessors synthesize backing delegate fields using `Delegate.Combine` and `Delegate.Remove`.
- Property setters notify event listeners when changed (e.g., `CheckBox.CheckedChanged`, `NumericUpDown.ValueChanged`, `TextBox.TextChanged`).

### 6. Zero `throw null` Guarantee
- Ref assemblies contain `throw null;` in every method body. `StubGenerator` replaces every instruction sequence in all methods, constructors, indexers, and interface implementations with valid IL returning `default`, `null`, or a newly initialized instance.

---

## Support Library: `LiveSplit.Compat.WinForms.Support`

Located under `linux/tools/WinFormsStubGen/LiveSplit.Compat.WinForms.Support/`. This project contains runtime helpers referenced by the generated stub:
- `StubSupport.cs`:
  - In-memory collection backing store (`_collections`) and collection ownership tracking (`_collectionOwners`).
  - Indexer helpers (`colGetItem`, `colSetItem`).
  - Property clamp helpers (`ClampDecimal`, `ClampInt`).
  - Event dispatch triggers (`FireEvent`).
  - Synchronous `Invoke` and `BeginInvoke` execution.
  - In-memory clipboard store and environment path properties (`StartupPath`, `ExecutablePath`).
- `IDeviceContext.cs`:
  - Definition of `System.Drawing.IDeviceContext` to satisfy interface references in controls without requiring `System.Drawing.Common`.

---

## Test Suite: `WinFormsStubGen.Tests`

Located under `linux/tools/WinFormsStubGen.Tests/`. Uses xUnit and Roslyn C# compilation:

1. **`SanityValidationTests.AllSignaturesResolve_WithoutUnexpectedMissingTypes`**:
   Loads the generated stub in an isolated `AssemblyLoadContext`, enumerates all types, and walks all methods, properties, events, and fields to ensure every signature resolves without unexpected loader exceptions.
2. **`SanityValidationTests.NoMethodThrowsNull_InGeneratedAssembly`**:
   Reads the generated stub via Mono.Cecil and verifies that no method contains a `throw` opcode.
3. **`DesignerSettingsTests.DesignerSettingsClass_InstantiatesAndRoundsTripsValues`**:
   Dynamically compiles a Windows Forms designer settings `UserControl` containing `TableLayoutPanel`, `CheckBox`, `NumericUpDown`, `TextBox`, `ComboBox`, `Button`, `GroupBox`, `FlowLayoutPanel`, and `DataBindings`. Verifies round-trip properties, clamping, event handlers firing on change, and hierarchical control collections.
4. **`ComponentDllsTests.AllWinFormsReferencesInComponents_ResolveAgainstStubOutput`**:
   Reads external component DLLs specified by `$LIVESPLIT_TEST_COMPONENT_DLLS` using `System.Reflection.Metadata` (without loading or executing untrusted code), checking all `System.Windows.Forms` `TypeRef` and `MemberRef` entries against the stub to verify zero missing types and zero missing members.

### Running the Tests

```bash
export PATH=$HOME/.dotnet:$PATH
export DOTNET_ROOT=$HOME/.dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export LIVESPLIT_TEST_COMPONENT_DLLS="/tmp/claude-1000/-home-luna/a784986e-7ae8-4c15-85c3-9d4e4bec4cf4/scratchpad/comp-dlls"

dotnet test linux/tools/WinFormsStubGen.Tests/WinFormsStubGen.Tests.csproj
```
