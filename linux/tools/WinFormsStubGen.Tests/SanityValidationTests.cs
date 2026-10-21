using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;
using Xunit.Abstractions;

namespace WinFormsStubGen.Tests;

public class SanityValidationTests : IClassFixture<StubFixture>
{
    private readonly StubFixture _fixture;
    private readonly ITestOutputHelper _output;

    public SanityValidationTests(StubFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private static readonly HashSet<string> AllowedMissingPrefixes = new()
    {
        "System.Drawing.Printing",
        "System.Drawing.ToolboxBitmapAttribute",
        "System.Drawing.IDeviceContext",
        "System.Drawing.BitmapSuffixInSatelliteAssemblyAttribute"
    };

    private static bool IsAllowedMissing(string typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return false;
        if (typeName.Contains("System.Drawing.Common")) return true;
        foreach (var prefix in AllowedMissingPrefixes)
        {
            if (typeName.Contains(prefix)) return true;
        }
        return false;
    }

    [Fact]
    public void AllSignaturesResolve_WithoutUnexpectedMissingTypes()
    {
        var asm = _fixture.StubAssembly;
        Assert.NotNull(asm);

        Type[] types;
        try
        {
            types = asm.GetTypes();
        }
        catch (ReflectionTypeLoadException rtle)
        {
            types = rtle.Types!;
            foreach (var ex in rtle.LoaderExceptions!)
            {
                if (ex == null) continue;
                _output.WriteLine($"LoaderException: {ex.Message}");
                // If the loader exception is about an allowed missing type, that's fine
                Assert.True(IsAllowedMissing(ex.Message), $"Unexpected type loader exception: {ex.Message}");
            }
        }

        int typesChecked = 0;
        int membersChecked = 0;
        var unexpectedErrors = new List<string>();

        foreach (var type in types)
        {
            if (type == null) continue;
            typesChecked++;

            try
            {
                _ = type.BaseType;
                _ = type.GetInterfaces();
            }
            catch (Exception ex)
            {
                if (!IsAllowedMissing(ex.Message))
                    unexpectedErrors.Add($"Type {type.FullName} hierarchy error: {ex.Message}");
            }

            MethodBase[] methods = Array.Empty<MethodBase>();
            try
            {
                methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            }
            catch (Exception ex)
            {
                if (!IsAllowedMissing(ex.Message))
                    unexpectedErrors.Add($"Type {type.FullName} GetMethods error: {ex.Message}");
            }

            foreach (var m in methods)
            {
                membersChecked++;
                try
                {
                    if (m is MethodInfo mi) _ = mi.ReturnType;
                    foreach (var p in m.GetParameters())
                    {
                        _ = p.ParameterType;
                    }
                }
                catch (Exception ex)
                {
                    if (!IsAllowedMissing(ex.Message))
                        unexpectedErrors.Add($"Method {type.FullName}::{m.Name} error: {ex.Message}");
                }
            }

            try
            {
                foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    membersChecked++;
                    _ = p.PropertyType;
                }
            }
            catch (Exception ex)
            {
                if (!IsAllowedMissing(ex.Message))
                    unexpectedErrors.Add($"Type {type.FullName} GetProperties error: {ex.Message}");
            }

            try
            {
                foreach (var e in type.GetEvents(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    membersChecked++;
                    _ = e.EventHandlerType;
                }
            }
            catch (Exception ex)
            {
                if (!IsAllowedMissing(ex.Message))
                    unexpectedErrors.Add($"Type {type.FullName} GetEvents error: {ex.Message}");
            }

            try
            {
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    membersChecked++;
                    _ = f.FieldType;
                }
            }
            catch (Exception ex)
            {
                if (!IsAllowedMissing(ex.Message))
                    unexpectedErrors.Add($"Type {type.FullName} GetFields error: {ex.Message}");
            }
        }

        _output.WriteLine($"Sanity check passed: {typesChecked} types and {membersChecked} members inspected.");
        if (unexpectedErrors.Count > 0)
        {
            foreach (var err in unexpectedErrors)
            {
                _output.WriteLine("FAIL: " + err);
            }
        }

        Assert.Empty(unexpectedErrors);
    }

    [Fact]
    public void NoMethodThrowsNull_InGeneratedAssembly()
    {
        var asmDef = Mono.Cecil.AssemblyDefinition.ReadAssembly(_fixture.StubDllPath);
        var throwMethods = new List<string>();

        void CheckType(Mono.Cecil.TypeDefinition t)
        {
            foreach (var m in t.Methods)
            {
                if (m.HasBody)
                {
                    foreach (var instr in m.Body.Instructions)
                    {
                        if (instr.OpCode == Mono.Cecil.Cil.OpCodes.Throw)
                        {
                            throwMethods.Add($"{t.FullName}::{m.Name}");
                            break;
                        }
                    }
                }
            }
            foreach (var nt in t.NestedTypes) CheckType(nt);
        }

        foreach (var t in asmDef.MainModule.Types)
        {
            CheckType(t);
        }

        _output.WriteLine($"Found {throwMethods.Count} methods that throw");
        foreach (var m in throwMethods.Take(30))
        {
            _output.WriteLine("  THROWS: " + m);
        }

        Assert.Empty(throwMethods);
    }
}
