using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using Xunit.Abstractions;

namespace WinFormsStubGen.Tests;

public class DefaultValuesAndTreeSemanticsTests : IClassFixture<StubFixture>
{
    private readonly StubFixture _fixture;
    private readonly ITestOutputHelper _output;

    public DefaultValuesAndTreeSemanticsTests(StubFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
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
            MetadataReference.CreateFromFile(Path.Combine(netCoreShared, "System.Collections.NonGeneric.dll")),
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
                .Select(d => d.ToString()));
            throw new InvalidOperationException($"Compilation failed:\n{errors}");
        }

        ms.Seek(0, SeekOrigin.Begin);
        return _fixture.Alc.LoadFromStream(ms);
    }

    [Fact]
    public void Requirement1_FormAndControl_VisibleDefaults_AndShowHideClose()
    {
        string code = @"
using System;
using System.Windows.Forms;

public class CustomForm : Form {}
public class CustomPanel : Panel {}

public class VisibleRunner
{
    public void Run()
    {
        // 1. Ordinary controls default to Visible = true
        var ctrl = new Control();
        if (!ctrl.Visible) throw new Exception(""Control.Visible must default to true"");

        var btn = new Button();
        if (!btn.Visible) throw new Exception(""Button.Visible must default to true"");

        var panel = new CustomPanel();
        if (!panel.Visible) throw new Exception(""Panel.Visible must default to true"");

        // 2. Forms default to Visible = false
        var form = new Form();
        if (form.Visible) throw new Exception(""Form.Visible must default to false"");

        var customForm = new CustomForm();
        if (customForm.Visible) throw new Exception(""CustomForm.Visible must default to false"");

        // 3. ToolStripDropDown and ContextMenuStrip default to Visible = false
        var tsdd = new ToolStripDropDown();
        if (tsdd.Visible) throw new Exception(""ToolStripDropDown.Visible must default to false"");

        var cms = new ContextMenuStrip();
        if (cms.Visible) throw new Exception(""ContextMenuStrip.Visible must default to false"");

        // 4. Show / Hide / Close / ShowDialog transitions
        form.Show();
        if (!form.Visible) throw new Exception(""Form.Show() must set Visible = true"");

        form.Hide();
        if (form.Visible) throw new Exception(""Form.Hide() must set Visible = false"");

        form.Visible = true;
        if (!form.Visible) throw new Exception(""Form.Visible = true must set Visible = true"");

        form.Close();
        if (form.Visible) throw new Exception(""Form.Close() must set Visible = false"");

        var dlgRes = form.ShowDialog();
        if (!form.Visible) throw new Exception(""Form.ShowDialog() must set Visible = true"");
        if (dlgRes != DialogResult.Cancel) throw new Exception(""Form.ShowDialog() must return Cancel"");

        tsdd.Show();
        if (!tsdd.Visible) throw new Exception(""ToolStripDropDown.Show() must set Visible = true"");
        tsdd.Close();
        if (tsdd.Visible) throw new Exception(""ToolStripDropDown.Close() must set Visible = false"");
    }
}
";
        var asm = CompileAssembly(code, "VisibleTests");
        var runnerType = asm.GetType("VisibleRunner")!;
        dynamic runner = Activator.CreateInstance(runnerType)!;
        runner.Run();
    }

    [Fact]
    public void Requirement2_ControlFont_DefaultsToSegoeUI9pt_Caches_AndOverrides()
    {
        string code = @"
using System;
using System.Drawing;
using System.Windows.Forms;

public class FontRunner
{
    public void Run()
    {
        var ctrl = new Button();
        var f1 = ctrl.Font;
        if (f1 == null) throw new Exception(""Control.Font must not be null"");
        if (f1.Name != ""Segoe UI"") throw new Exception($""Control.Font.Name must be Segoe UI, got {f1.Name}"");
        if (Math.Abs(f1.Size - 9.0f) > 0.01f) throw new Exception($""Control.Font.Size must be 9.0, got {f1.Size}"");

        // Cached per control
        var f2 = ctrl.Font;
        if (!object.ReferenceEquals(f1, f2)) throw new Exception(""Control.Font must cache per control instance"");

        // Setter overrides
        var custom = new Font(""Arial"", 12.0f);
        ctrl.Font = custom;
        if (!object.ReferenceEquals(ctrl.Font, custom)) throw new Exception(""Control.Font setter must override"");

        // Control.DefaultFont static property
        var def = Control.DefaultFont;
        if (def == null) throw new Exception(""Control.DefaultFont must not be null"");
        if (def.Name != ""Segoe UI"") throw new Exception($""Control.DefaultFont.Name must be Segoe UI, got {def.Name}"");

        // DataGridView.Font inherits Control
        var dgv = new DataGridView();
        if (dgv.Font == null) throw new Exception(""DataGridView.Font must not be null"");
        if (dgv.Font.Name != ""Segoe UI"") throw new Exception(""DataGridView.Font must inherit Control.Font"");

        // DataGridViewCellStyle.Font is legit null unless explicitly set
        var style = new DataGridViewCellStyle();
        if (style.Font != null) throw new Exception(""DataGridViewCellStyle.Font must default to null"");
        style.Font = custom;
        if (!object.ReferenceEquals(style.Font, custom)) throw new Exception(""DataGridViewCellStyle.Font setter must work"");
    }
}
";
        var asm = CompileAssembly(code, "FontTests");
        var runnerType = asm.GetType("FontRunner")!;
        dynamic runner = Activator.CreateInstance(runnerType)!;
        runner.Run();
    }

    [Fact]
    public void Requirement3_LazyCreatedGetters_NeverNull_AndTypedCells()
    {
        string code = @"
using System;
using System.Windows.Forms;

public class LazyRunner
{
    public void Run()
    {
        var dgv = new DataGridView();
        if (dgv.RowTemplate == null) throw new Exception(""DataGridView.RowTemplate must not be null"");
        if (!(dgv.RowTemplate is DataGridViewRow)) throw new Exception(""RowTemplate must be DataGridViewRow"");

        var col = new DataGridViewColumn();
        if (col.HeaderCell == null) throw new Exception(""DataGridViewColumn.HeaderCell must not be null"");
        if (!(col.HeaderCell is DataGridViewColumnHeaderCell)) throw new Exception(""HeaderCell must be DataGridViewColumnHeaderCell"");

        // Typed column cell templates
        var textCol = new DataGridViewTextBoxColumn();
        if (textCol.CellTemplate == null) throw new Exception(""DataGridViewTextBoxColumn.CellTemplate must not be null"");
        if (!(textCol.CellTemplate is DataGridViewTextBoxCell)) throw new Exception(""textCol.CellTemplate must be DataGridViewTextBoxCell"");

        var checkCol = new DataGridViewCheckBoxColumn();
        if (checkCol.CellTemplate == null) throw new Exception(""DataGridViewCheckBoxColumn.CellTemplate must not be null"");
        if (!(checkCol.CellTemplate is DataGridViewCheckBoxCell)) throw new Exception(""checkCol.CellTemplate must be DataGridViewCheckBoxCell"");

        var comboCol = new DataGridViewComboBoxColumn();
        if (comboCol.CellTemplate == null) throw new Exception(""DataGridViewComboBoxColumn.CellTemplate must not be null"");
        if (!(comboCol.CellTemplate is DataGridViewComboBoxCell)) throw new Exception(""comboCol.CellTemplate must be DataGridViewComboBoxCell"");

        var btnCol = new DataGridViewButtonColumn();
        if (btnCol.CellTemplate == null) throw new Exception(""DataGridViewButtonColumn.CellTemplate must not be null"");
        if (!(btnCol.CellTemplate is DataGridViewButtonCell)) throw new Exception(""btnCol.CellTemplate must be DataGridViewButtonCell"");

        // ButtonBase FlatAppearance
        var btn = new Button();
        if (btn.FlatAppearance == null) throw new Exception(""ButtonBase.FlatAppearance must not be null"");
        if (!(btn.FlatAppearance is FlatButtonAppearance)) throw new Exception(""FlatAppearance must be FlatButtonAppearance"");

        // ScrollableControl scroll properties
        var panel = new Panel();
        if (panel.VerticalScroll == null) throw new Exception(""VerticalScroll must not be null"");
        if (!(panel.VerticalScroll is VScrollProperties)) throw new Exception(""VerticalScroll must be VScrollProperties"");
        if (panel.HorizontalScroll == null) throw new Exception(""HorizontalScroll must not be null"");
        if (!(panel.HorizontalScroll is HScrollProperties)) throw new Exception(""HorizontalScroll must be HScrollProperties"");

        // CurrentCell stays legit null
        if (dgv.CurrentCell != null) throw new Exception(""DataGridView.CurrentCell must default to null"");
    }
}
";
        var asm = CompileAssembly(code, "LazyTests");
        var runnerType = asm.GetType("LazyRunner")!;
        dynamic runner = Activator.CreateInstance(runnerType)!;
        runner.Run();
    }

    [Fact]
    public void Requirement4_StaticSingletons_Cursors_Screen_Application_SystemInformation_SystemColors()
    {
        string code = @"
using System;
using System.Drawing;
using System.Windows.Forms;

public class SingletonRunner
{
    public void Run()
    {
        // 1. Cursors
        if (Cursors.SizeAll == null) throw new Exception(""Cursors.SizeAll must not be null"");
        if (Cursors.Hand == null) throw new Exception(""Cursors.Hand must not be null"");
        if (Cursors.Default == null) throw new Exception(""Cursors.Default must not be null"");
        if (Cursors.WaitCursor == null) throw new Exception(""Cursors.WaitCursor must not be null"");
        if (Cursors.Arrow == null) throw new Exception(""Cursors.Arrow must not be null"");
        if (Cursor.Current == null) throw new Exception(""Cursor.Current must not be null"");

        // 2. Screen
        var prim = Screen.PrimaryScreen;
        if (prim == null) throw new Exception(""Screen.PrimaryScreen must not be null"");
        if (!prim.Primary) throw new Exception(""Screen.PrimaryScreen.Primary must be true"");
        if (prim.DeviceName != ""DISPLAY1"") throw new Exception($""Screen DeviceName must be DISPLAY1, got {prim.DeviceName}"");
        if (prim.Bounds.Width != 1920 || prim.Bounds.Height != 1080) throw new Exception($""Screen.Bounds must be 1920x1080, got {prim.Bounds}"");
        if (prim.WorkingArea.Width != 1920 || prim.WorkingArea.Height != 1080) throw new Exception($""Screen.WorkingArea must be 1920x1080, got {prim.WorkingArea}"");

        var all = Screen.AllScreens;
        if (all == null || all.Length == 0) throw new Exception(""Screen.AllScreens must return non-empty array"");
        if (!object.ReferenceEquals(all[0], prim)) throw new Exception(""Screen.AllScreens[0] must be PrimaryScreen"");

        var ctrl = new Control();
        if (!object.ReferenceEquals(Screen.FromControl(ctrl), prim)) throw new Exception(""Screen.FromControl must return PrimaryScreen"");
        if (!object.ReferenceEquals(Screen.FromPoint(new Point(50, 50)), prim)) throw new Exception(""Screen.FromPoint must return PrimaryScreen"");
        if (!object.ReferenceEquals(Screen.FromRectangle(new Rectangle(0, 0, 10, 10)), prim)) throw new Exception(""Screen.FromRectangle must return PrimaryScreen"");
        if (Screen.GetBounds(ctrl).Width != 1920) throw new Exception(""Screen.GetBounds must be 1920"");
        if (Screen.GetWorkingArea(ctrl).Width != 1920) throw new Exception(""Screen.GetWorkingArea must be 1920"");

        // 3. Application.OpenForms
        var openForms = Application.OpenForms;
        if (openForms == null) throw new Exception(""Application.OpenForms must not be null"");
        if (openForms.Count != 0) throw new Exception(""Application.OpenForms must be empty"");

        // 4. SystemInformation
        if (SystemInformation.WorkingArea.Width != 1920 || SystemInformation.WorkingArea.Height != 1080)
            throw new Exception($""SystemInformation.WorkingArea must be 1920x1080, got {SystemInformation.WorkingArea}"");
        if (SystemInformation.PrimaryMonitorSize.Width != 1920 || SystemInformation.PrimaryMonitorSize.Height != 1080)
            throw new Exception($""SystemInformation.PrimaryMonitorSize must be 1920x1080, got {SystemInformation.PrimaryMonitorSize}"");
        if (SystemInformation.VirtualScreen.Width != 1920 || SystemInformation.VirtualScreen.Height != 1080)
            throw new Exception($""SystemInformation.VirtualScreen must be 1920x1080, got {SystemInformation.VirtualScreen}"");
        if (SystemInformation.BorderSize.Width != 1 || SystemInformation.BorderSize.Height != 1)
            throw new Exception($""SystemInformation.BorderSize must be 1x1, got {SystemInformation.BorderSize}"");
        if (SystemInformation.CaptionHeight != 23)
            throw new Exception($""SystemInformation.CaptionHeight must be 23, got {SystemInformation.CaptionHeight}"");
        if (SystemInformation.VerticalScrollBarWidth != 17)
            throw new Exception($""SystemInformation.VerticalScrollBarWidth must be 17, got {SystemInformation.VerticalScrollBarWidth}"");
        if (!SystemInformation.MousePresent)
            throw new Exception(""SystemInformation.MousePresent must be true"");
        if (!SystemInformation.UserInteractive)
            throw new Exception(""SystemInformation.UserInteractive must be true"");

        // 5. SystemColors
        if (SystemColors.Window.A != 255) throw new Exception(""SystemColors.Window must not be transparent"");
        if (SystemColors.WindowText.A != 255) throw new Exception(""SystemColors.WindowText must not be transparent"");
        if (SystemColors.Control.A != 255) throw new Exception(""SystemColors.Control must not be transparent"");
        if (SystemColors.ControlText.A != 255) throw new Exception(""SystemColors.ControlText must not be transparent"");
        if (SystemColors.Highlight.A != 255) throw new Exception(""SystemColors.Highlight must not be transparent"");
        if (SystemColors.HighlightText.A != 255) throw new Exception(""SystemColors.HighlightText must not be transparent"");
        if (SystemColors.GrayText.A != 255) throw new Exception(""SystemColors.GrayText must not be transparent"");
    }
}
";
        var asm = CompileAssembly(code, "SingletonTests");
        var runnerType = asm.GetType("SingletonRunner")!;
        dynamic runner = Activator.CreateInstance(runnerType)!;
        runner.Run();
    }

    [Fact]
    public void Requirement5_TreeSemantics_Hierarchy_Navigation_Bounds_AndSelectedNode()
    {
        string code = @"
using System;
using System.Drawing;
using System.Windows.Forms;

public class TreeRunner
{
    public int beforeCount = 0;
    public object beforeNode = null;
    public int afterCount = 0;
    public object afterNode = null;
    public bool shouldCancel = false;

    public void Run()
    {
        var tv = new TreeView();
        if (tv.Nodes == null) throw new Exception(""tv.Nodes must not be null"");
        if (tv.Nodes.Count != 0) throw new Exception(""Nodes.Count must start at 0"");

        // Add(text)
        var root1 = tv.Nodes.Add(""Root 1"");
        if (root1 == null) throw new Exception(""Nodes.Add(text) must return node"");
        if (root1.Text != ""Root 1"") throw new Exception(""root1.Text mismatch"");
        if (root1.Index != 0) throw new Exception($""root1.Index must be 0, got {root1.Index}"");
        if (root1.Level != 0) throw new Exception($""root1.Level must be 0, got {root1.Level}"");
        if (root1.FullPath != ""Root 1"") throw new Exception($""root1.FullPath must be 'Root 1', got {root1.FullPath}"");
        if (root1.Parent != null) throw new Exception(""root1.Parent must be null"");
        if (!object.ReferenceEquals(root1.TreeView, tv)) throw new Exception(""root1.TreeView must be tv"");

        // Add(key, text)
        var root2 = tv.Nodes.Add(""k2"", ""Root 2"");
        if (root2.Name != ""k2"") throw new Exception($""root2.Name must be k2, got {root2.Name}"");
        if (root2.Text != ""Root 2"") throw new Exception(""root2.Text mismatch"");
        if (root2.Index != 1) throw new Exception($""root2.Index must be 1, got {root2.Index}"");
        if (!object.ReferenceEquals(root2.TreeView, tv)) throw new Exception(""root2.TreeView must be tv"");

        // Add children to root1
        var c1 = root1.Nodes.Add(""Child 1"");
        var c2 = root1.Nodes.Add(""Child 2"");
        if (c1.Parent != root1) throw new Exception(""c1.Parent must be root1"");
        if (c1.TreeView != tv) throw new Exception(""c1.TreeView must be tv"");
        if (c1.Level != 1) throw new Exception($""c1.Level must be 1, got {c1.Level}"");
        if (c1.FullPath != ""Root 1\\Child 1"") throw new Exception($""c1.FullPath must be Root 1\\Child 1, got {c1.FullPath}"");
        if (c2.Index != 1) throw new Exception($""c2.Index must be 1, got {c2.Index}"");

        // Detached node joining TreeView recursively updates TreeView on descendants
        var standaloneParent = new TreeNode(""Detached"");
        var detachedChild = standaloneParent.Nodes.Add(""Detached Child"");
        if (detachedChild.TreeView != null) throw new Exception(""detachedChild.TreeView must be null before join"");
        if (detachedChild.Parent != standaloneParent) throw new Exception(""detachedChild.Parent must be standaloneParent"");

        tv.Nodes.Add(standaloneParent);
        if (standaloneParent.TreeView != tv) throw new Exception(""standaloneParent.TreeView must be tv"");
        if (detachedChild.TreeView != tv) throw new Exception(""detachedChild.TreeView must be updated to tv on parent join"");

        // Navigation
        if (!object.ReferenceEquals(root1.NextNode, root2)) throw new Exception(""root1.NextNode must be root2"");
        if (!object.ReferenceEquals(root2.PrevNode, root1)) throw new Exception(""root2.PrevNode must be root1"");
        if (!object.ReferenceEquals(root1.FirstNode, c1)) throw new Exception(""root1.FirstNode must be c1"");
        if (!object.ReferenceEquals(root1.LastNode, c2)) throw new Exception(""root1.LastNode must be c2"");

        // Expand / Collapse / Toggle
        if (root1.IsExpanded) throw new Exception(""root1.IsExpanded must start false"");
        root1.Expand();
        if (!root1.IsExpanded) throw new Exception(""root1.Expand() must set IsExpanded true"");
        root1.Collapse();
        if (root1.IsExpanded) throw new Exception(""root1.Collapse() must set IsExpanded false"");
        root1.Toggle();
        if (!root1.IsExpanded) throw new Exception(""root1.Toggle() must set IsExpanded true"");

        // Bounds
        var b1 = root1.Bounds;
        if (b1.Width <= 0 || b1.Height <= 0) throw new Exception(""TreeNode.Bounds must be non-empty"");
        var b2 = root2.Bounds;
        if (b2.Y <= b1.Y) throw new Exception(""root2.Bounds.Y must be greater than root1.Bounds.Y"");

        // Find(key, searchAllChildren)
        var found = tv.Nodes.Find(""k2"", true);
        if (found == null || found.Length != 1) throw new Exception(""Find must find 1 matching node"");
        if (!object.ReferenceEquals(found[0], root2)) throw new Exception(""Find result mismatch"");

        // SelectedNode and Events
        tv.BeforeSelect += (s, e) =>
        {
            beforeCount++;
            beforeNode = e.Node;
            if (shouldCancel) e.Cancel = true;
        };
        tv.AfterSelect += (s, e) =>
        {
            afterCount++;
            afterNode = e.Node;
        };

        tv.SelectedNode = root1;
        if (beforeCount != 1) throw new Exception($""BeforeSelect must fire once, got {beforeCount}"");
        if (!object.ReferenceEquals(beforeNode, root1)) throw new Exception(""BeforeSelect.Node must not be null and match root1"");
        if (afterCount != 1) throw new Exception($""AfterSelect must fire once, got {afterCount}"");
        if (!object.ReferenceEquals(afterNode, root1)) throw new Exception(""AfterSelect.Node must not be null and match root1"");
        if (!object.ReferenceEquals(tv.SelectedNode, root1)) throw new Exception(""tv.SelectedNode must be root1"");

        // Setting same value again must NOT fire
        tv.SelectedNode = root1;
        if (beforeCount != 1 || afterCount != 1) throw new Exception(""Duplicate SelectedNode set must NOT re-fire events"");

        // Setting new value fires
        tv.SelectedNode = root2;
        if (beforeCount != 2 || afterCount != 2) throw new Exception(""Changing SelectedNode must fire events"");
        if (!object.ReferenceEquals(afterNode, root2)) throw new Exception(""AfterSelect.Node must match root2"");

        // Cancelable BeforeSelect
        shouldCancel = true;
        tv.SelectedNode = c1;
        if (beforeCount != 3) throw new Exception(""BeforeSelect must fire on cancel attempt"");
        if (afterCount != 2) throw new Exception(""AfterSelect must NOT fire when BeforeSelect was canceled"");
        if (!object.ReferenceEquals(tv.SelectedNode, root2)) throw new Exception(""SelectedNode must revert when canceled"");

        // TreeView CheckBoxes and Checked
        tv.CheckBoxes = true;
        root1.Checked = true;
        if (!root1.Checked) throw new Exception(""root1.Checked must be true"");
    }
}
";
        var asm = CompileAssembly(code, "TreeTests");
        var runnerType = asm.GetType("TreeRunner")!;
        dynamic runner = Activator.CreateInstance(runnerType)!;
        runner.Run();
    }

    [Fact]
    public void Requirement6_GenericCollection_AddOverloads_ConstructElements_AndEventArgsProps()
    {
        string code = @"
using System;
using System.Windows.Forms;

public class GenericColRunner
{
    public void Run()
    {
        // 1. LinkLabel.LinkCollection.Add(int, int, object) and (int, int)
        var ll = new LinkLabel();
        if (ll.Links == null) throw new Exception(""ll.Links must not be null"");

        var link1 = ll.Links.Add(0, 5, ""https://livesplit.org"");
        if (link1 == null) throw new Exception(""LinkCollection.Add(int, int, object) must return Link"");
        if (!(link1 is LinkLabel.Link)) throw new Exception(""Returned object must be LinkLabel.Link"");
        if (link1.Start != 0) throw new Exception($""link1.Start must be 0, got {link1.Start}"");
        if (link1.Length != 5) throw new Exception($""link1.Length must be 5, got {link1.Length}"");
        if ((string)link1.LinkData != ""https://livesplit.org"") throw new Exception(""link1.LinkData mismatch"");
        if (ll.Links.Count != 1) throw new Exception(""ll.Links.Count must be 1"");
        if (!object.ReferenceEquals(ll.Links[0], link1)) throw new Exception(""ll.Links[0] must be link1"");

        var link2 = ll.Links.Add(10, 8);
        if (link2 == null) throw new Exception(""LinkCollection.Add(int, int) must return Link"");
        if (link2.Start != 10 || link2.Length != 8) throw new Exception(""link2 parameters mismatch"");
        if (ll.Links.Count != 2) throw new Exception(""ll.Links.Count must be 2"");

        // 2. EventArgs classes populate properties from constructor parameters
        var node = new TreeNode(""Sample"");
        var tvea = new TreeViewEventArgs(node, TreeViewAction.ByMouse);
        if (!object.ReferenceEquals(tvea.Node, node)) throw new Exception(""TreeViewEventArgs.Node must be populated"");
        if (tvea.Action != TreeViewAction.ByMouse) throw new Exception(""TreeViewEventArgs.Action must be populated"");

        var tvcea = new TreeViewCancelEventArgs(node, true, TreeViewAction.ByKeyboard);
        if (!object.ReferenceEquals(tvcea.Node, node)) throw new Exception(""TreeViewCancelEventArgs.Node must be populated"");
        if (!tvcea.Cancel) throw new Exception(""TreeViewCancelEventArgs.Cancel must be populated"");
        if (tvcea.Action != TreeViewAction.ByKeyboard) throw new Exception(""TreeViewCancelEventArgs.Action must be populated"");

        var llcea = new LinkLabelLinkClickedEventArgs(link1, MouseButtons.Left);
        if (!object.ReferenceEquals(llcea.Link, link1)) throw new Exception(""LinkLabelLinkClickedEventArgs.Link must be populated"");
        if (llcea.Button != MouseButtons.Left) throw new Exception(""LinkLabelLinkClickedEventArgs.Button must be populated"");

        var lvi = new ListViewItem(""TestItem"");
        var lvia = new ListViewItemSelectionChangedEventArgs(lvi, 7, true);
        if (!object.ReferenceEquals(lvia.Item, lvi)) throw new Exception(""ListViewItemSelectionChangedEventArgs.Item must be populated"");
        if (lvia.ItemIndex != 7) throw new Exception(""ListViewItemSelectionChangedEventArgs.ItemIndex must be populated"");
        if (!lvia.IsSelected) throw new Exception(""ListViewItemSelectionChangedEventArgs.IsSelected must be populated"");
    }
}
";
        var asm = CompileAssembly(code, "GenericColTests");
        var runnerType = asm.GetType("GenericColRunner")!;
        dynamic runner = Activator.CreateInstance(runnerType)!;
        runner.Run();
    }
}
