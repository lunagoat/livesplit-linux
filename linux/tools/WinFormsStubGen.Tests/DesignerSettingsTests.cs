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

        // Reference needed assemblies
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT") 
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet");
        var netCoreShared = Path.Combine(dotnetRoot, "shared", "Microsoft.NETCore.App", "10.0.13");

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(Path.Combine(netCoreShared, "System.Private.CoreLib.dll")),
            MetadataReference.CreateFromFile(Path.Combine(netCoreShared, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(netCoreShared, "System.Collections.dll")),
            MetadataReference.CreateFromFile(Path.Combine(netCoreShared, "System.Drawing.Primitives.dll")),
            MetadataReference.CreateFromFile(Path.Combine(netCoreShared, "System.ComponentModel.Primitives.dll")),
            MetadataReference.CreateFromFile(Path.Combine(netCoreShared, "System.ComponentModel.TypeConverter.dll")),
            MetadataReference.CreateFromFile(_fixture.CoreDllPath),
            MetadataReference.CreateFromFile(_fixture.DrawingDllPath),
            MetadataReference.CreateFromFile(_fixture.StubDllPath)
        };

        var compilation = CSharpCompilation.Create(
            "DesignerTestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var emitResult = compilation.Emit(ms);

        if (!emitResult.Success)
        {
            foreach (var diag in emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                _output.WriteLine($"Compilation error: {diag.GetMessage()}");
            }
        }
        Assert.True(emitResult.Success, "Roslyn compilation of class S failed.");

        ms.Seek(0, SeekOrigin.Begin);
        var testAsm = _fixture.Alc.LoadFromStream(ms);
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
}
