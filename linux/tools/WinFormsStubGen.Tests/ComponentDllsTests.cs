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

public class ComponentDllsTests : IClassFixture<StubFixture>
{
    private readonly StubFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ComponentDllsTests(StubFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void AllWinFormsReferencesInComponents_ResolveAgainstStubOutput()
    {
        string? compDir = Environment.GetEnvironmentVariable("LIVESPLIT_TEST_COMPONENT_DLLS");
        if (string.IsNullOrEmpty(compDir) || !Directory.Exists(compDir))
        {
            _output.WriteLine("Skipping component DLLs test: $LIVESPLIT_TEST_COMPONENT_DLLS is not set or directory does not exist.");
            return;
        }

        var stubAsm = _fixture.StubAssembly;
        var coreAsm = _fixture.Alc.LoadFromAssemblyPath(_fixture.CoreDllPath);

        // Preload type cache from stub and core
        var typeCache = new Dictionary<string, Type>();
        void CacheTypes(Assembly asm)
        {
            try
            {
                foreach (var t in asm.GetTypes())
                {
                    typeCache[t.FullName!] = t;
                    if (t.FullName!.Contains('+'))
                    {
                        typeCache[t.FullName.Replace('+', '/')] = t;
                    }
                }
            }
            catch (ReflectionTypeLoadException rtle)
            {
                foreach (var t in rtle.Types)
                {
                    if (t != null)
                    {
                        typeCache[t.FullName!] = t;
                        if (t.FullName!.Contains('+'))
                        {
                            typeCache[t.FullName.Replace('+', '/')] = t;
                        }
                    }
                }
            }
        }

        CacheTypes(stubAsm);
        CacheTypes(coreAsm);
        if (_fixture.ViewAssembly != null)
        {
            CacheTypes(_fixture.ViewAssembly);
        }

        int totalDlls = 0;
        int managedDlls = 0;
        int totalTypeRefs = 0;
        int totalMemberRefs = 0;

        var missingTypes = new HashSet<string>();
        var missingMembers = new HashSet<string>();

        foreach (var dllPath in Directory.EnumerateFiles(compDir, "*.dll", SearchOption.AllDirectories))
        {
            totalDlls++;
            try
            {
                using var fs = File.OpenRead(dllPath);
                using var pe = new PEReader(fs);
                if (!pe.HasMetadata) continue;
                var reader = pe.GetMetadataReader();
                managedDlls++;

                var typeRefNames = new Dictionary<TypeReferenceHandle, string>();

                foreach (var handle in reader.TypeReferences)
                {
                    var tr = reader.GetTypeReference(handle);
                    string ns = reader.GetString(tr.Namespace);
                    string name = reader.GetString(tr.Name);

                    string fullName;
                    if (string.IsNullOrEmpty(ns) && tr.ResolutionScope.Kind == HandleKind.TypeReference)
                    {
                        var parentTr = reader.GetTypeReference((TypeReferenceHandle)tr.ResolutionScope);
                        string parentNs = reader.GetString(parentTr.Namespace);
                        string parentName = reader.GetString(parentTr.Name);
                        fullName = $"{parentNs}.{parentName}/{name}";
                    }
                    else
                    {
                        fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
                    }

                    typeRefNames[handle] = fullName;

                    bool isWatched = fullName.StartsWith("System.Windows.Forms")
                        || fullName.StartsWith("LiveSplit.View")
                        || fullName == "LiveSplit.Properties.Resources";

                    if (isWatched)
                    {
                        totalTypeRefs++;
                        if (!TryResolveType(fullName, stubAsm, coreAsm, _fixture.ViewAssembly, typeCache, out _))
                        {
                            missingTypes.Add(fullName);
                        }
                    }
                }

                foreach (var handle in reader.MemberReferences)
                {
                    var mr = reader.GetMemberReference(handle);
                    string memberName = reader.GetString(mr.Name);

                    string? typeFullName = null;
                    if (mr.Parent.Kind == HandleKind.TypeReference)
                    {
                        var trHandle = (TypeReferenceHandle)mr.Parent;
                        if (typeRefNames.TryGetValue(trHandle, out var fn))
                        {
                            typeFullName = fn;
                        }
                    }

                    bool isWatched = typeFullName != null && (
                        typeFullName.StartsWith("System.Windows.Forms")
                        || typeFullName.StartsWith("LiveSplit.View")
                        || typeFullName == "LiveSplit.Properties.Resources");

                    if (isWatched)
                    {
                        totalMemberRefs++;
                        if (TryResolveType(typeFullName!, stubAsm, coreAsm, _fixture.ViewAssembly, typeCache, out var targetType))
                        {
                            if (!MemberExists(targetType, memberName))
                            {
                                missingMembers.Add($"{typeFullName}::{memberName}");
                            }
                        }
                        else
                        {
                            missingMembers.Add($"{typeFullName}::{memberName} (type missing)");
                        }
                    }
                }
            }
            catch
            {
                // Unmanaged or invalid DLL
            }
        }

        _output.WriteLine("================ COMPONENT DLL MISS REPORT ================");
        _output.WriteLine($"Total DLLs scanned: {totalDlls} (Managed: {managedDlls})");
        _output.WriteLine($"WinForms TypeRefs checked: {totalTypeRefs}");
        _output.WriteLine($"WinForms MemberRefs checked: {totalMemberRefs}");
        _output.WriteLine($"Missing Types count: {missingTypes.Count}");
        foreach (var mt in missingTypes.OrderBy(x => x))
        {
            _output.WriteLine($"  [MISSING TYPE] {mt}");
        }

        _output.WriteLine($"Missing Members count: {missingMembers.Count}");
        foreach (var mm in missingMembers.OrderBy(x => x))
        {
            _output.WriteLine($"  [MISSING MEMBER] {mm}");
        }
        _output.WriteLine("===========================================================");

        Assert.True(missingTypes.Count == 0, $"Found {missingTypes.Count} missing WinForms types in component DLLs.");
        Assert.True(missingMembers.Count == 0, $"Found {missingMembers.Count} missing WinForms members in component DLLs.");
    }

    private static bool TryResolveType(string fullName, Assembly stubAsm, Assembly coreAsm, Assembly? viewAsm, Dictionary<string, Type> typeCache, out Type targetType)
    {
        if (typeCache.TryGetValue(fullName, out targetType!)) return true;
        string plusName = fullName.Replace('/', '+');
        if (typeCache.TryGetValue(plusName, out targetType!)) return true;

        try
        {
            targetType = stubAsm.GetType(fullName) ?? stubAsm.GetType(plusName)
                ?? coreAsm.GetType(fullName) ?? coreAsm.GetType(plusName)
                ?? (viewAsm?.GetType(fullName) ?? viewAsm?.GetType(plusName))!;
        }
        catch (TypeLoadException)
        {
            targetType = stubAsm.DefinedTypes.FirstOrDefault(t => t.FullName == fullName || t.FullName == plusName)
                ?? coreAsm.DefinedTypes.FirstOrDefault(t => t.FullName == fullName || t.FullName == plusName)
                ?? (viewAsm?.DefinedTypes.FirstOrDefault(t => t.FullName == fullName || t.FullName == plusName))!;
        }

        if (targetType != null)
        {
            typeCache[fullName] = targetType;
            return true;
        }

        return false;
    }

    private static bool MemberExists(Type type, string memberName)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;

        try
        {
            if (type.GetMember(memberName, flags).Length > 0) return true;
            if (type.GetProperty(memberName, flags) != null) return true;
            if (type.GetField(memberName, flags) != null) return true;
            if (type.GetMethod(memberName, flags) != null) return true;
            if (type.GetEvent(memberName, flags) != null) return true;
        }
        catch { }

        try
        {
            if (type is TypeInfo ti)
            {
                if (ti.DeclaredMembers.Any(m => m.Name == memberName)) return true;
            }
        }
        catch { }

        try
        {
            // Also check interfaces
            foreach (var iface in type.GetInterfaces())
            {
                if (iface.GetMember(memberName, flags).Length > 0) return true;
            }
        }
        catch { }

        return false;
    }
}
