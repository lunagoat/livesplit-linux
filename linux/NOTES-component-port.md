# Porting a LiveSplit component to net10.0 (toolkit free)

Recipe written from the pilot (Timer, Title, Text, BlankSpace). A component with one settings page takes ~20 minutes.
Rendering code (`Draw*`, `Update`, `PrepareDraw`, labels, brushes) is NOT touched: the SkiaSharp `System.Drawing` shim and Core already compile it unchanged.
What changes is the csproj, the settings class (no more `UserControl`), and `GetSettingsControl`.

Result per component: `XSettings : LiveSplit.UI.SettingsBase` (plain model, `INotifyPropertyChanged`), no Designer/resx, a spec in `linux/ui-specs/X.md`
(the contract for the Avalonia view), and tests in `test/LiveSplit.Components.Tests`.

## Checklist

1. Read `X.csproj`, `XSettings.cs`, `XSettings.Designer.cs`, `X.cs` (and any other `*.cs` that uses `System.Windows.Forms`, `MessageBox`, `Application`, `Control`).
2. Write `linux/ui-specs/X.md` BEFORE deleting the designer (see "UI spec"). The old files stay readable with `git show HEAD:<path>` until the deletion is committed, `git show 9f61153~1:<path>` is not enough for these (they were deleted in the pilot, not in 9f61153).
3. Replace the csproj with the template below.
4. Delete `XSettings.Designer.cs`, `XSettings.resx` (and any other resx/designer of dialogs that go away).
5. Rewrite `XSettings.cs` as a plain model (see "Settings class").
6. `X.cs`: `public Control GetSettingsControl(...)` becomes `public object GetSettingsControl(LayoutMode mode)`; delete `using System.Windows.Forms;`. Everything else stays.
7. `dotnet build` the component. Fix what the compiler finds (see "Routing Win32/WinForms leftovers").
8. Normalize every edited `.cs` to UTF-8 BOM + CRLF (the repo convention, see pitfalls).
9. Register: add the csproj to `linux/LiveSplit.Linux.slnx` (`/components/` folder, alphabetical) and as a `ComponentProject` line in `linux/LiveSplit.App/LiveSplit.App.csproj` (the app owner may already do this).
10. Tests: add a project reference to `test/LiveSplit.Components.Tests/LiveSplit.Components.Tests.csproj`, add the dll name to `TestSupport.EnsureFactories`, add a settings round trip + a render test (see "Tests"). LOOK at the PNGs.
11. `dotnet test test/LiveSplit.Components.Tests`, `dotnet build linux/LiveSplit.Linux.slnx`.

## csproj template (exact)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>LiveSplit</RootNamespace>
    <TargetFramework>net10.0</TargetFramework>
    <NoWarn>$(NoWarn);CA1416;CS0618</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="$(LsSrcPath)/LiveSplit.Core/LiveSplit.Core.csproj" Private="false" ExcludeAssets="runtime" />
  </ItemGroup>

</Project>
```

Differences to the Windows csproj: `net4.8.1` -> `net10.0`; `UseWindowsForms` gone; `UpdateManager` reference gone (`IUpdateable` lives in Core now); `CustomFontDialog` reference gone (font/colour pickers belong to the UI layer);
`PolySharp` gone (not needed on net10.0); `EnableDynamicLoading` gone (see pitfalls). Keep a reference to another component only when the code really uses its types (DetailedTimer uses `Timer`):
`<ProjectReference Include="$(ComponentsPath)/LiveSplit.Timer/src/LiveSplit.Timer/LiveSplit.Timer.csproj" Private="false" ExcludeAssets="runtime" />`.
`LsSrcPath`/`LsLibPath`/`ComponentsPath` come from `components/Directory.Build.props` (unchanged, except for the new `ExcludeHostAssembliesFromOutput` target below).

Output goes to `artifacts/build/Components/<X>.dll` (`components/Directory.Build.props`). The pattern `Private=false ExcludeAssets=runtime` works with the app: `ComponentManager` loads `Components/*.dll` into the default
`AssemblyLoadContext`, where Core, the Drawing shim and SpeedrunComSharp already are. Core's transitive dependencies (`LiveSplit.Drawing.dll`, `SpeedrunComSharp.dll`) used to be copied next to the component anyway;
`components/Directory.Build.props` now has a target `ExcludeHostAssembliesFromOutput` that removes `LiveSplit.Core/LiveSplit.Drawing/SpeedrunComSharp` from the copy-local list, so a component's output folder holds only its own dll (+ pdb, deps.json).
If a component has its own NuGet dependencies, add them as `PackageReference`s and set `<EnableDynamicLoading>true</EnableDynamicLoading>` (copies the package assemblies next to the component dll).

## Settings class

Keep ONE class with the old name (`TimerSettings`), derive from `LiveSplit.UI.SettingsBase` (Core, `UI/SettingsBase.cs`):

```csharp
public abstract class SettingsBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null); // true when changed
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null);
}
```

Rules:

* Every settable property becomes a backing field + `set => SetField(ref field, value)`. Setters of properties that other computed properties depend on raise those too:
  `if (SetField(ref x, value)) { OnPropertyChanged(nameof(IsColor1Visible)); }`. Same-value assignments do not notify. Constructor defaults assign through the properties (nobody is subscribed yet), so behaviour is unchanged.
* `GetSettings(XmlDocument)`, `SetSettings(XmlNode)`, `GetSettingsHashCode()`, version handling and the `CreateSettingsNode` code stay verbatim. Helpers the component calls (`GetBackgroundTypeString`) stay.
* No base class from WinForms, no `InitializeComponent`, no `DataBindings`, no `*_Click`/`*_Changed`/`*_Load`/`*_Format` handlers, no `ColorButtonClick`.
* Whatever the old event handlers derived from the controls moves into the model as READ-ONLY computed properties plus change notifications, so the Avalonia view only binds:

| Old WinForms code | Model |
|---|---|
| `label.Enabled = button.Enabled = chkOverride.Checked` | `public bool IsTimerColorEnabled => OverrideSplitColors;` (notified from the `OverrideSplitColors` setter) |
| `btnColor1.Visible = type != "Plain" && !type.Contains("Delta")` | `IsColor1Visible`, `IsColor2Visible` (notified from the gradient setter) |
| `btnColor2.DataBindings.Add("BackColor", this, btnColor1.Visible ? "BackgroundColor2" : "BackgroundColor")` | `BackgroundSwatch2Color` get/set: edits `BackgroundColor2` when `IsColor1Visible`, else `BackgroundColor` |
| `Load` handler that picks trackbar min/max/binding/label from `Mode` | `Mode` (notifies), `SizeValue` (maps to `TimerWidth`/`TimerHeight`), `SizeMinimum`, `SizeMaximum`, `SizeLabel` (already `T(...)` translated) |
| `chkTwoRows.Enabled = false; Checked = true` in horizontal mode | `IsDisplay2RowsEnabled => Mode != Horizontal`, `Display2RowsShown` (the real property is not modified) |
| `label4.Text = CustomVariable ? "Custom Variable Name:" : "Text:"` | `Text2Label` |
| combo `Items.AddRange(...)` | `public static readonly string[] DigitsFormatOptions` ... (display strings; for enum combos a `GradientString` string property accepts them) |
| `cmbX.SelectedIndex = (int)Alignment` | `TextAlignmentIndex` int property next to the enum property |

Gotcha: `Mode` is `LayoutMode.Horizontal` (enum value 0) until `GetSettingsControl(mode)` sets it. This matches the old `Load` handler (`if (Mode == Horizontal) ... else ...`).
Settings that are legacy / never shown in the UI (fonts of the old font override) stay as properties (they are read by `SetSettings`), they only need the `SetField` setter.

### Worked example: Timer

Before (`TimerSettings.cs`, WinForms):

```csharp
public partial class TimerSettings : UserControl
{
    public float TimerHeight { get; set; }
    public Color BackgroundColor { get; set; }
    public DeltasGradientType BackgroundGradient { get; set; }

    public TimerSettings()
    {
        InitializeComponent();
        TimerHeight = 50;
        ...
        btnTimerColor.DataBindings.Add("BackColor", this, "TimerColor", false, DataSourceUpdateMode.OnPropertyChanged);
        chkOverrideTimerColors.DataBindings.Add("Checked", this, "OverrideSplitColors", ...);
        ...
    }

    private void cmbGradientType_SelectedIndexChanged(object sender, EventArgs e)
    {
        string selectedText = cmbGradientType.SelectedItem.ToString();
        btnColor1.Visible = selectedText != "Plain" && !selectedText.Contains("Delta");
        btnColor2.Visible = !selectedText.Contains("Delta");
        btnColor2.DataBindings.Clear();
        btnColor2.DataBindings.Add("BackColor", this, btnColor1.Visible ? "BackgroundColor2" : "BackgroundColor", ...);
        BackgroundGradient = (DeltasGradientType)cmbGradientType.SelectedItem;
    }
    ... SetSettings / GetSettings / CreateSettingsNode ...
    private void ColorButtonClick(object sender, EventArgs e) { SettingsHelper.ColorButtonClick((Button)sender, this); }
}
```

After (`TimerSettings.cs`, toolkit free):

```csharp
public class TimerSettings : SettingsBase
{
    public static readonly string[] GradientOptions = [.. Enum.GetValues<DeltasGradientType>().Select(GetBackgroundTypeString)];

    private float timerHeight;
    private DeltasGradientType backgroundGradient;
    ...
    public float TimerHeight
    {
        get => timerHeight;
        set { if (SetField(ref timerHeight, value)) { OnPropertyChanged(nameof(SizeValue)); } }
    }

    public DeltasGradientType BackgroundGradient
    {
        get => backgroundGradient;
        set
        {
            if (SetField(ref backgroundGradient, value))
            {
                OnPropertyChanged(nameof(GradientString));
                OnPropertyChanged(nameof(IsColor1Visible));
                OnPropertyChanged(nameof(IsColor2Visible));
                OnPropertyChanged(nameof(BackgroundSwatch2Color));
            }
        }
    }

    public bool IsColor1Visible => BackgroundGradient is not DeltasGradientType.Plain && !GradientString.Contains("Delta");
    public bool IsColor2Visible => !GradientString.Contains("Delta");
    public Color BackgroundSwatch2Color { get => IsColor1Visible ? BackgroundColor2 : BackgroundColor; set { ... } }

    public TimerSettings() { TimerWidth = 225; TimerHeight = 50; ... }   // same defaults as before, no control wiring

    // SetSettings / GetSettings / GetSettingsHashCode / CreateSettingsNode: unchanged
}
```

`Timer.cs` (rendering untouched): only `public Control GetSettingsControl(LayoutMode mode)` -> `public object GetSettingsControl(LayoutMode mode)` (`Settings.Mode = mode; return Settings;`), and `using System.Windows.Forms;` removed.
Components whose old `GetSettingsControl` did not set `Mode` (Title) keep returning the settings object as is.

## UI spec (`linux/ui-specs/X.md`)

Terse, from Designer + code-behind, in control order: for each control the label text (they are translation keys, `UiLocalizer.Translate`), kind (checkbox / number min,max,step / color swatch / combo with items / font / textbox / slider / file), the bound model property, enable/visibility dependency (name the computed property), group boxes and tabs, tooltips (title + text), WinForms commit semantics when not immediate, and which settings are never shown. See the four existing specs for the style.
Dig the Designer for: `Items.AddRange`, `Minimum/Maximum/Increment/DecimalPlaces`, `ToolTip.SetToolTip`, `Enabled`, `DataBindings` and `*_Changed` handlers in the code-behind (these hide most of the dependencies).

## Routing Win32/WinForms leftovers

* Dialogs: `MessageBox.Show` does not exist; `PlatformServices.Current.ShowMessage(owner, text, caption, buttons, icon)`. `DialogResult`, `MessageBoxButtons`, `MessageBoxIcon`, `Keys` exist in Core (namespace `System.Windows.Forms`, no WinForms).
* Prompts: `InputBox.Show(owner, ...)` / `LoginBox` / `PlatformServices.Current.Prompt`, file pickers `ChooseOpenFile`/`ChooseSaveFile`, URLs `OpenUrl`, clipboard `SetClipboardText/Image`.
* Colour/font pickers and `SettingsHelper.ColorButtonClick` are gone: the view owns them. Fonts are `System.Drawing.Font` properties on the model, read/written with `SettingsHelper.GetFontFromElement` / `CreateSetting(doc, parent, name, font)` (unchanged API).
* `Control`-derived components: `ControlComponent(state, object control, ...)`, `IFormHost`; see `NOTES-core-api-changes.md`. File paths from shared layouts go through `PathCompat.Resolve`.
* Anything missing: report it to the lead, do not stub it silently.

## Tests

`test/LiveSplit.Components.Tests` (xunit; PNGs go to `$LS_COMPONENT_TEST_OUT`, default `/tmp/livesplit-component-tests`):

* `SettingsMust`: round trip (`GetSettings` -> new instance `SetSettings` -> same XML string and `GetSettingsHashCode`), default values, old version files, and the generic theory that sets EVERY settable property by reflection and asserts `PropertyChanged` fires with that name (add the type to `SettingsTypes`; extend `Different` for new property types).
* `LayoutLoadingMust`: loads real Windows XML through `XMLLayoutFactory` with the factories registered by `TestSupport.EnsureFactories()` (copies only the component dlls to a temp `Components/` folder and points `ComponentManager.BasePath` at it, exactly like the app). The Windows default layout is copied to `Samples/DefaultLayout.lsl`.
* `RenderingMust`: `TestSupport.CreateState()` (3 segments, game/category, attempts, optional icon), `MakeRunning(state, elapsed, splitIndex)`, `TestSupport.Render(state, components, mode, fixedSize, "x.png")` runs the real `ComponentRenderer` (Update + Render, warm-up passes, bitmap sized to the layout's natural size) and saves a PNG. Assert a few pixels, then open the PNG with the Read tool and check it visually.

## Pitfalls hit

* Line endings/encoding: the repo files are UTF-8 BOM + CRLF (`.editorconfig`). Python `open().read()` converts CRLF to LF silently and `sed '/^using X;$/d'` does not match `;\r`. Use `sed '/^using X;\r\?$/d'` and finish with a normalizer (read bytes, strip BOM, `\r\n`->`\n`->`\r\n`, prepend BOM) over every touched `.cs`.
* `EnableDynamicLoading=true` turns on copy-local for all dependencies and drags `LiveSplit.Drawing.dll`, `SpeedrunComSharp.dll` and SkiaSharp `runtimes/` into `Components/`. Not needed for components that only use Core.
* `XMLLayoutFactory` THROWS ("X.dll could not be found") for a `<Component>` whose factory is not registered, it does not skip it. Tests that load the default layout must drop the not yet ported components from the XML first.
* `new StandardSettingsFactory().Create()` needs `ComponentManager.RaceProviderFactories` non-null (NRE otherwise); `TestSupport.EnsureFactories()` sets it.
* Components size themselves from text they measured during drawing (Title `HorizontalWidth`, Text/InfoText `VerticalHeight`); measure the layout size only after a couple of Update+Render passes, not before the first draw.
* `ComponentManager.LoadFactory` swallows every exception (`catch {}`): a component whose assembly cannot load simply does not appear. If a factory is missing, instantiate the factory type directly in a test to see the real error.
* `Namespace LiveSplit.Hosting` shadows a type named `Platform`; see `NOTES-core-api-changes.md`.
* The shim's font fallbacks (Segoe UI -> Noto Sans, Century Gothic -> URW Gothic) make text a little wider/taller than on Windows: do not assert exact pixel widths of text.
* Other components may depend on a ported component's settings object (DetailedTimer drives `Timer.Settings.*` every frame): keep every old public property and its type.
