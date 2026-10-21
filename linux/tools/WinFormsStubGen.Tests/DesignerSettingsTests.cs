using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;

namespace WinFormsStubGen.Tests;

public class DesignerSettingsTests : IClassFixture<StubFixture>
{
    private readonly StubFixture _fixture;
    private readonly ITestOutputHelper _output;

    public DesignerSettingsTests(StubFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void DesignerSettingsClass_InstantiatesAndRoundsTripsValues()
    {
        string sourceCode = @"
using System;
using System.Drawing;
using System.Windows.Forms;

public class TestModel
{
    public bool Prop { get; set; } = true;
}

public class S : System.Windows.Forms.UserControl
{
    public CheckBox c;
    public NumericUpDown n;
    public Label l;
    public TableLayoutPanel t;
    public TextBox tb;
    public ComboBox cb;
    public Button b;
    public GroupBox g;
    public FlowLayoutPanel f;
    public bool checkedChangedFired;

    public S()
    {
        SuspendLayout();
        c = new CheckBox();
        n = new NumericUpDown();
        l = new Label();
        t = new TableLayoutPanel();
        tb = new TextBox();
        cb = new ComboBox();
        b = new Button();
        g = new GroupBox();
        f = new FlowLayoutPanel();

        t.Controls.Add(c, 0, 1);
        g.Controls.Add(l);
        Controls.Add(t);

        c.Name = ""chkSplit"";
        c.Text = ""Split on Chapter Complete"";
        c.Size = new Size(100, 20);
        c.Location = new Point(10, 10);
        c.Dock = DockStyle.Fill;
        c.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        c.AutoSize = true;
        c.TabIndex = 1;
        c.Margin = new Padding(3);
        c.Padding = new Padding(2);
        c.Font = new Font(""Arial"", 9f);

        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        t.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));

        c.CheckedChanged += (sender, e) => { checkedChangedFired = true; };
        c.Checked = true;

        n.Minimum = 0;
        n.Maximum = 100;
        n.Value = 150; // Should clamp to 100

        cb.Items.AddRange(new object[] { ""a"", ""b"" });
        cb.SelectedIndex = 1;

        var obj = new TestModel();
        c.DataBindings.Add(""Checked"", obj, ""Prop"");

        ResumeLayout(false);
    }
}
";

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var testAsm = CompileAssembly(sourceCode, "DesignerTestAssembly");
        var sType = testAsm.GetType("S");
        Assert.NotNull(sType);

        // Instantiate S
        dynamic s = Activator.CreateInstance(sType)!;
        Assert.NotNull(s);

        // Assertions
        Assert.Equal("chkSplit", (string)s.c.Name);
        Assert.Equal("Split on Chapter Complete", (string)s.c.Text);
        Assert.Equal(100, (int)s.c.Size.Width);
        Assert.Equal(20, (int)s.c.Size.Height);
        Assert.Equal(10, (int)s.c.Location.X);
        Assert.Equal(10, (int)s.c.Location.Y);
        Assert.True((bool)s.c.AutoSize);
        Assert.Equal(1, (int)s.c.TabIndex);
        Assert.Equal(3, (int)s.c.Margin.All);
        Assert.Equal(2, (int)s.c.Padding.All);
        Assert.Equal("Arial", (string)s.c.Font.Name);

        // Event handler fired on checked change
        Assert.True((bool)s.c.Checked);
        Assert.True((bool)s.checkedChangedFired);

        // NumericUpDown value clamped to 100
        Assert.Equal(0m, (decimal)s.n.Minimum);
        Assert.Equal(100m, (decimal)s.n.Maximum);
        Assert.Equal(100m, (decimal)s.n.Value);

        // ComboBox items and selection
        Assert.Equal(2, (int)s.cb.Items.Count);
        Assert.Equal("a", (string)s.cb.Items[0]);
        Assert.Equal("b", (string)s.cb.Items[1]);
        Assert.Equal(1, (int)s.cb.SelectedIndex);
        Assert.Equal("b", (string)s.cb.SelectedItem);

        // Controls hierarchy and counts
        Assert.Equal(1, (int)s.Controls.Count);
        Assert.Equal(1, (int)s.t.Controls.Count);
        Assert.Equal(1, (int)s.g.Controls.Count);
        Assert.Same(s.c, s.t.Controls[0]);
        Assert.Same(s.l, s.g.Controls[0]);
        Assert.Same(s.t, s.Controls[0]);

        _output.WriteLine($"s.t={s.t}, s.c={s.c}, s.c.Parent={s.c.Parent}");
        Assert.Same(s.t, s.c.Parent);
        Assert.Same(s.g, s.l.Parent);
        Assert.Same(s, s.t.Parent);

        // TableLayoutPanel styles
        Assert.Equal(1, (int)s.t.ColumnStyles.Count);
        Assert.Equal(1, (int)s.t.RowStyles.Count);

        _output.WriteLine("Designer settings class verification passed completely!");
    }

    private Assembly CompileAssembly(string sourceCode, string assemblyName = "TestAssembly")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var netCoreShared = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(Path.Combine(netCoreShared, "System.Private.CoreLib.dll")),
            MetadataReference.CreateFromFile(Path.Combine(netCoreShared, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(netCoreShared, "System.Collections.dll")),
            MetadataReference.CreateFromFile(Path.Combine(netCoreShared, "System.Drawing.Primitives.dll")),
            MetadataReference.CreateFromFile(Path.Combine(netCoreShared, "System.ComponentModel.dll")),
            MetadataReference.CreateFromFile(Path.Combine(netCoreShared, "System.ComponentModel.Primitives.dll")),
            MetadataReference.CreateFromFile(Path.Combine(netCoreShared, "System.ComponentModel.TypeConverter.dll")),
            MetadataReference.CreateFromFile(_fixture.CoreDllPath),
            MetadataReference.CreateFromFile(_fixture.DrawingDllPath),
            MetadataReference.CreateFromFile(_fixture.StubDllPath)
        };
        if (File.Exists(_fixture.ViewDllPath))
        {
            references.Add(MetadataReference.CreateFromFile(_fixture.ViewDllPath));
        }

        var compilation = CSharpCompilation.Create(
            assemblyName + "_" + Guid.NewGuid().ToString("N"),
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            var errors = string.Join("\n", emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage()));
            throw new InvalidOperationException($"Roslyn compilation failed:\n{errors}");
        }

        ms.Seek(0, SeekOrigin.Begin);
        return _fixture.Alc.LoadFromStream(ms);
    }

    [Fact]
    public void ChangeEvents_FireOnlyWhenChanged_AndReentrancyGuardPreventsStackOverflow()
    {
        string code = @"
using System;
using System.Windows.Forms;

public class EventRunner
{
    public int checkChangedFired = 0;
    public int textChangedFired = 0;
    public int valueChangedFired = 0;
    public int reentrancyDepthReached = 0;

    public void RunChangeOnly()
    {
        var cb = new CheckBox();
        cb.CheckedChanged += (s, e) => checkChangedFired++;
        cb.Checked = true;  // fires (false -> true)
        cb.Checked = true;  // duplicate: must NOT fire
        cb.Checked = true;  // duplicate: must NOT fire

        var tb = new TextBox();
        tb.TextChanged += (s, e) => textChangedFired++;
        tb.Text = ""hello""; // fires
        tb.Text = ""hello""; // duplicate: must NOT fire

        var nud = new NumericUpDown();
        nud.ValueChanged += (s, e) => valueChangedFired++;
        nud.Value = 42m;     // fires
        nud.Value = 42m;     // duplicate: must NOT fire
    }

    public void RunReentrant()
    {
        var cb = new CheckBox();
        cb.CheckedChanged += (s, e) =>
        {
            reentrancyDepthReached++;
            // Handler re-enters by mutating checked state
            cb.Checked = !cb.Checked;
        };

        // Trigger first change
        cb.Checked = true;
    }

    public void RunThrowingHandler()
    {
        var cb = new CheckBox();
        cb.CheckedChanged += (s, e) =>
        {
            throw new InvalidOperationException(""DirectExceptionMessage"");
        };
        cb.Checked = true;
    }
}
";
        var asm = CompileAssembly(code, "EventTests");
        var runnerType = asm.GetType("EventRunner")!;
        dynamic runner = Activator.CreateInstance(runnerType)!;

        runner.RunChangeOnly();
        Assert.Equal(1, (int)runner.checkChangedFired);
        Assert.Equal(1, (int)runner.textChangedFired);
        Assert.Equal(1, (int)runner.valueChangedFired);

        runner.RunReentrant();
        int depth = (int)runner.reentrancyDepthReached;
        _output.WriteLine($"Reentrancy depth reached before guard terminated: {depth}");
        Assert.True(depth >= 5 && depth <= 12, $"Expected reentrancy depth around ~8, got {depth}");

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            try
            {
                runner.RunThrowingHandler();
            }
            catch (TargetInvocationException tie) when (tie.InnerException is InvalidOperationException ioe)
            {
                throw ioe;
            }
        });
        Assert.Equal("DirectExceptionMessage", ex.Message);
    }

    [Fact]
    public void Collections_RoundTrip_SupportListOperations()
    {
        string code = @"
using System;
using System.Collections;
using System.Windows.Forms;

public class CollectionRunner
{
    public void Run()
    {
        var lb = new ListBox();
        var items = lb.Items;
        if (items == null) throw new Exception(""Items collection is null"");

        items.Add(""Item 1"");
        items.AddRange(new object[] { ""Item 2"", ""Item 3"" });

        if (items.Count != 3) throw new Exception(""Expected Count 3, got "" + items.Count);
        if ((string)items[0] != ""Item 1"") throw new Exception(""Index 0 mismatch"");
        if ((string)items[1] != ""Item 2"") throw new Exception(""Index 1 mismatch"");
        if ((string)items[2] != ""Item 3"") throw new Exception(""Index 2 mismatch"");

        if (!items.Contains(""Item 2"")) throw new Exception(""Contains returned false"");
        if (items.IndexOf(""Item 2"") != 1) throw new Exception(""IndexOf mismatch"");

        items.Insert(1, ""Inserted Item"");
        if (items.Count != 4) throw new Exception(""Expected Count 4 after insert"");
        if ((string)items[1] != ""Inserted Item"") throw new Exception(""Inserted item mismatch"");

        items.Remove(""Inserted Item"");
        if (items.Count != 3) throw new Exception(""Expected Count 3 after remove"");

        items.RemoveAt(0);
        if (items.Count != 2) throw new Exception(""Expected Count 2 after removeAt"");
        if ((string)items[0] != ""Item 2"") throw new Exception(""Remaining item mismatch"");

        // CopyTo
        var arr = new object[2];
        items.CopyTo(arr, 0);
        if ((string)arr[0] != ""Item 2"" || (string)arr[1] != ""Item 3"")
            throw new Exception(""CopyTo failed"");

        // Enumerator foreach
        int enumeratedCount = 0;
        foreach (var item in items)
        {
            enumeratedCount++;
        }
        if (enumeratedCount != 2) throw new Exception(""Foreach count mismatch"");

        items.Clear();
        if (items.Count != 0) throw new Exception(""Expected Count 0 after Clear"");
    }
}
";
        var asm = CompileAssembly(code, "ColTests");
        var runnerType = asm.GetType("CollectionRunner")!;
        dynamic runner = Activator.CreateInstance(runnerType)!;
        runner.Run();
    }

    [Fact]
    public void DataGridView_RowsColumnsAndCells_RoundTrip()
    {
        string code = @"
using System;
using System.Windows.Forms;

public class DgvRunner
{
    public void Run()
    {
        var dgv = new DataGridView();
        if (dgv.Rows == null) throw new Exception(""dgv.Rows is null"");
        if (dgv.Columns == null) throw new Exception(""dgv.Columns is null"");

        // Add columns via string and typed objects
        int c0 = dgv.Columns.Add(""Col1"", ""Header 1"");
        var textCol = new DataGridViewTextBoxColumn { Name = ""Col2"", HeaderText = ""Header 2"" };
        int c1 = dgv.Columns.Add(textCol);
        var checkCol = new DataGridViewCheckBoxColumn { Name = ""Col3"", HeaderText = ""Header 3"" };
        int c2 = dgv.Columns.Add(checkCol);
        var comboCol = new DataGridViewComboBoxColumn { Name = ""Col4"", HeaderText = ""Header 4"" };
        int c3 = dgv.Columns.Add(comboCol);

        if (dgv.Columns.Count != 4) throw new Exception(""Columns.Count expected 4, got "" + dgv.Columns.Count);

        // Add row via params object[]
        int r0 = dgv.Rows.Add(""Val1"", ""Val2"", true, ""OptionA"");
        if (r0 != 0) throw new Exception(""Row index expected 0, got "" + r0);
        if (dgv.Rows.Count != 1) throw new Exception(""Rows.Count expected 1, got "" + dgv.Rows.Count);

        var row = dgv.Rows[0];
        if (row.Cells == null) throw new Exception(""row.Cells is null"");

        // Access by int index
        if ((string)row.Cells[0].Value != ""Val1"") throw new Exception(""Cells[0] value mismatch"");
        if ((string)row.Cells[1].Value != ""Val2"") throw new Exception(""Cells[1] value mismatch"");

        // Access by string column name
        if ((string)row.Cells[""Col1""].Value != ""Val1"") throw new Exception(""Cells['Col1'] value mismatch"");
        if ((string)row.Cells[""Col2""].Value != ""Val2"") throw new Exception(""Cells['Col2'] value mismatch"");

        // Mutate cell value
        row.Cells[""Col1""].Value = ""UpdatedVal1"";
        if ((string)row.Cells[0].Value != ""UpdatedVal1"") throw new Exception(""Updated cell value mismatch"");

        // RowCount / ColumnCount setters
        dgv.RowCount = 5;
        if (dgv.Rows.Count != 5) throw new Exception(""RowCount setter failed, Rows.Count is "" + dgv.Rows.Count);
        if (dgv.RowCount != 5) throw new Exception(""RowCount getter failed, got "" + dgv.RowCount);

        dgv.ColumnCount = 7;
        if (dgv.Columns.Count != 7) throw new Exception(""ColumnCount setter failed, Columns.Count is "" + dgv.Columns.Count);
        if (dgv.ColumnCount != 7) throw new Exception(""ColumnCount getter failed, got "" + dgv.ColumnCount);
    }
}
";
        var asm = CompileAssembly(code, "DgvTests");
        var runnerType = asm.GetType("DgvRunner")!;
        dynamic runner = Activator.CreateInstance(runnerType)!;
        runner.Run();
    }

    [Fact]
    public void ContextMenu_MenuItems_Add_AndRoundTrip()
    {
        string code = @"
using System;
using System.Windows.Forms;

public class MenuRunner
{
    public void Run()
    {
        var menu = new ContextMenu();
        if (menu.MenuItems == null) throw new Exception(""MenuItems is null"");

        var item1 = menu.MenuItems.Add(""Item 1"");
        if (item1 == null) throw new Exception(""MenuItems.Add(string) returned null"");
        if (item1.Text != ""Item 1"") throw new Exception(""item1.Text mismatch"");

        var item2 = new MenuItem(""Item 2"");
        int idx2 = menu.MenuItems.Add(item2);

        if (menu.MenuItems.Count != 2) throw new Exception(""Expected 2 menu items, got "" + menu.MenuItems.Count);
        if (menu.MenuItems[0].Text != ""Item 1"") throw new Exception(""Item 0 Text mismatch"");
        if (menu.MenuItems[1].Text != ""Item 2"") throw new Exception(""Item 1 Text mismatch"");
        if (menu.MenuItems[1] != item2) throw new Exception(""Item 1 reference mismatch"");
    }
}
";
        var asm = CompileAssembly(code, "MenuTests");
        var runnerType = asm.GetType("MenuRunner")!;
        dynamic runner = Activator.CreateInstance(runnerType)!;
        runner.Run();
    }

    [Fact]
    public void ListView_ItemsAddString_AndItemProperties_RoundTrip()
    {
        string code = @"
using System;
using System.Windows.Forms;

public class ListViewRunner
{
    public void Run()
    {
        var lv = new ListView();
        if (lv.Items == null) throw new Exception(""lv.Items is null"");
        if (lv.Columns == null) throw new Exception(""lv.Columns is null"");

        // ListView.Items.Add(string) must return ListViewItem and store a ListViewItem
        var item = lv.Items.Add(""Entry A"");
        if (item == null) throw new Exception(""Items.Add returned null"");
        if (!(item is ListViewItem)) throw new Exception(""Items.Add did not return ListViewItem"");
        if (item.Text != ""Entry A"") throw new Exception(""item.Text mismatch"");

        // Verify enumerator returns ListViewItem (no InvalidCastException like sweep row)
        foreach (ListViewItem lvi in lv.Items)
        {
            if (lvi.Text != ""Entry A"") throw new Exception(""Enumerated item mismatch"");
        }

        // ListViewItem properties round trip
        item.Checked = true;
        item.Tag = ""CustomTag"";
        if (!item.Checked) throw new Exception(""item.Checked was false"");
        if ((string)item.Tag != ""CustomTag"") throw new Exception(""item.Tag mismatch"");

        // SubItems
        if (item.SubItems == null) throw new Exception(""SubItems is null"");
        item.SubItems.Add(""Sub 1"");
        if (item.SubItems.Count < 2) throw new Exception(""SubItems count mismatch"");
        if (item.SubItems[1].Text != ""Sub 1"") throw new Exception(""SubItems[1] mismatch"");

        // TreeView / TreeNode
        var tv = new TreeView();
        if (tv.Nodes == null) throw new Exception(""tv.Nodes is null"");
        var node = tv.Nodes.Add(""Root"");
        if (node == null) throw new Exception(""tv.Nodes.Add returned null"");
        if (!(node is TreeNode)) throw new Exception(""Nodes.Add did not return TreeNode"");
        if (node.Text != ""Root"") throw new Exception(""node.Text mismatch"");

        node.Checked = true;
        node.Tag = 999;
        if (!node.Checked) throw new Exception(""node.Checked mismatch"");
        if ((int)node.Tag != 999) throw new Exception(""node.Tag mismatch"");

        var child = node.Nodes.Add(""Child"");
        if (child == null) throw new Exception(""node.Nodes.Add returned null"");
        if (node.Nodes.Count != 1) throw new Exception(""Child nodes count mismatch"");
        if (node.Nodes[0].Text != ""Child"") throw new Exception(""Child text mismatch"");
    }
}
";
        var asm = CompileAssembly(code, "LvTests");
        var runnerType = asm.GetType("ListViewRunner")!;
        dynamic runner = Activator.CreateInstance(runnerType)!;
        runner.Run();
    }

    [Fact]
    public void LazyCreatedProperties_AllowAndDenyList()
    {
        string code = @"
using System;
using System.Drawing;
using System.Windows.Forms;

public class LazyCheckRunner
{
    public void CheckAllowList()
    {
        var dgv = new DataGridView();
        if (dgv.Rows == null) throw new Exception(""dgv.Rows must not be null"");
        if (dgv.Columns == null) throw new Exception(""dgv.Columns must not be null"");
        if (dgv.DefaultCellStyle == null) throw new Exception(""dgv.DefaultCellStyle must not be null"");

        var menu = new ContextMenu();
        if (menu.MenuItems == null) throw new Exception(""menu.MenuItems must not be null"");

        var ts = new ToolStrip();
        if (ts.Items == null) throw new Exception(""ts.Items must not be null"");

        var ms = new MenuStrip();
        if (ms.Items == null) throw new Exception(""ms.Items must not be null"");

        var tv = new TreeView();
        if (tv.Nodes == null) throw new Exception(""tv.Nodes must not be null"");

        var il = new ImageList();
        if (il.Images == null) throw new Exception(""il.Images must not be null"");

        var tc = new TabControl();
        if (tc.TabPages == null) throw new Exception(""tc.TabPages must not be null"");

        var sc = new SplitContainer();
        if (sc.Panel1 == null) throw new Exception(""sc.Panel1 must not be null"");
        if (sc.Panel2 == null) throw new Exception(""sc.Panel2 must not be null"");

        var lv = new ListView();
        if (lv.Columns == null) throw new Exception(""lv.Columns must not be null"");
        if (lv.Items == null) throw new Exception(""lv.Items must not be null"");
        if (lv.Groups == null) throw new Exception(""lv.Groups must not be null"");

        var c = new UserControl();
        if (c.DataBindings == null) throw new Exception(""c.DataBindings must not be null"");
    }

    public void CheckDenyList()
    {
        var c = new Button();
        if (c.ContextMenuStrip != null) throw new Exception(""c.ContextMenuStrip must remain null"");
        if (c.ContextMenu != null) throw new Exception(""c.ContextMenu must remain null"");
        if (c.Parent != null) throw new Exception(""c.Parent must remain null"");
        if (c.TopLevelControl != null) throw new Exception(""c.TopLevelControl must remain null"");
        if (c.Tag != null) throw new Exception(""c.Tag must remain null"");
        if (c.Site != null) throw new Exception(""c.Site must remain null"");
        if (c.Image != null) throw new Exception(""c.Image must remain null"");

        var lb = new ListBox();
        if (lb.DataSource != null) throw new Exception(""lb.DataSource must remain null"");

        var f = new Form();
        if (f.Owner != null) throw new Exception(""f.Owner must remain null"");
        if (f.ActiveMdiChild != null) throw new Exception(""f.ActiveMdiChild must remain null"");
    }
}
";
        var asm = CompileAssembly(code, "LazyTests");
        var runnerType = asm.GetType("LazyCheckRunner")!;
        dynamic runner = Activator.CreateInstance(runnerType)!;
        runner.CheckAllowList();
        runner.CheckDenyList();
    }

    [Fact]
    public void LiveSplitView_StubTypes_InstantiateAndVerifyMembers()
    {
        string code = @"
using System;
using System.Reflection;
using System.Windows.Forms;
using LiveSplit.View;
using LiveSplit.Properties;
using LiveSplit.UI.Components;

public class ViewRunner
{
    public void Run()
    {
        // RunEditorDialog derives from Form
        var red = new RunEditorDialog();
        if (!(red is Form)) throw new Exception(""RunEditorDialog must derive from Form"");
        // Verify SegmentList property is absent
        var segProp = typeof(RunEditorDialog).GetProperty(""SegmentList"");
        if (segProp != null) throw new Exception(""SegmentList property must not exist on RunEditorDialog"");

        // ComponentSettingsDialog derives from Form, has ctor with IComponent
        var csd = new ComponentSettingsDialog((LiveSplit.UI.Components.IComponent)null);
        if (!(csd is Form)) throw new Exception(""ComponentSettingsDialog must derive from Form"");

        // SpeedRunsLiveForm derives from Form
        var srl = new SpeedRunsLiveForm();
        if (!(srl is Form)) throw new Exception(""SpeedRunsLiveForm must derive from Form"");

        // Resources static properties return null Bitmap
        if (Resources.Add != null) throw new Exception(""Resources.Add must be null"");
        if (Resources.Remove != null) throw new Exception(""Resources.Remove must be null"");
        if (Resources.UpArrow != null) throw new Exception(""Resources.UpArrow must be null"");
        if (Resources.DownArrow != null) throw new Exception(""Resources.DownArrow must be null"");
    }
}
";
        var asm = CompileAssembly(code, "ViewTests");
        var runnerType = asm.GetType("ViewRunner")!;
        dynamic runner = Activator.CreateInstance(runnerType)!;
        runner.Run();
    }
}
