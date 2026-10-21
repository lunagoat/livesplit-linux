using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using WinFormsStubGen;

namespace WinFormsStubGen.Tests;

public class StubFixture : IDisposable
{
    public string TempDir { get; }
    public string StubDllPath { get; }
    public string CoreDllPath { get; }
    public string DrawingDllPath { get; }
    public string ViewDllPath { get; }
    public Assembly StubAssembly { get; }
    public Assembly? ViewAssembly { get; }
    public AssemblyLoadContext Alc { get; }

    public StubFixture()
    {
        TempDir = Path.Combine(Path.GetTempPath(), "WinFormsStubGen_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempDir);

        string refDir = Environment.GetEnvironmentVariable("WINFORMS_REF_DIR") 
            ?? "/tmp/claude-1000/-home-luna/a784986e-7ae8-4c15-85c3-9d4e4bec4cf4/scratchpad/ref/pkg/ref/net10.0";
        
        string baseDir = AppContext.BaseDirectory;
        // Search for LiveSplit.Core.dll and LiveSplit.Drawing.dll
        string coreDll = FindDll(baseDir, "LiveSplit.Core.dll");
        string drawingDll = FindDll(baseDir, "LiveSplit.Drawing.dll");

        var options = new GeneratorOptions
        {
            RefDir = refDir,
            CorePath = coreDll,
            DrawingPath = drawingDll,
            OutDir = TempDir
        };

        StubGenerator.Generate(options);

        StubDllPath = Path.Combine(TempDir, "LiveSplit.Compat.WinForms.dll");
        CoreDllPath = Path.Combine(TempDir, "LiveSplit.Core.dll");
        DrawingDllPath = Path.Combine(TempDir, "LiveSplit.Drawing.dll");
        ViewDllPath = Path.Combine(TempDir, "LiveSplit.View.dll");

        Alc = new AssemblyLoadContext("StubTestContext", isCollectible: true);
        Alc.Resolving += (context, assemblyName) =>
        {
            string candidate = Path.Combine(TempDir, assemblyName.Name + ".dll");
            if (File.Exists(candidate))
            {
                return context.LoadFromAssemblyPath(candidate);
            }
            return null;
        };

        StubAssembly = Alc.LoadFromAssemblyPath(StubDllPath);
        if (File.Exists(ViewDllPath))
        {
            ViewAssembly = Alc.LoadFromAssemblyPath(ViewDllPath);
        }
    }

    private static string FindDll(string startDir, string name)
    {
        string current = startDir;
        for (int i = 0; i < 6; i++)
        {
            string candidate = Path.Combine(current, name);
            if (File.Exists(candidate)) return candidate;

            string inArtifacts = Path.Combine(current, "artifacts", "build", "release", name);
            if (File.Exists(inArtifacts)) return inArtifacts;

            inArtifacts = Path.Combine(current, "artifacts", "bin", Path.GetFileNameWithoutExtension(name), "release", name);
            if (File.Exists(inArtifacts)) return inArtifacts;

            inArtifacts = Path.Combine(current, "artifacts", "build", name);
            if (File.Exists(inArtifacts)) return inArtifacts;

            inArtifacts = Path.Combine(current, "artifacts", "bin", Path.GetFileNameWithoutExtension(name), "debug", name);
            if (File.Exists(inArtifacts)) return inArtifacts;

            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        throw new FileNotFoundException($"Could not find {name} starting from {startDir}");
    }

    public void Dispose()
    {
        try
        {
            Alc.Unload();
        }
        catch { }

        try
        {
            if (Directory.Exists(TempDir))
                Directory.Delete(TempDir, true);
        }
        catch { }
    }
}
