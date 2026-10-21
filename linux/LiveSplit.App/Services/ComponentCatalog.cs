using LiveSplit.Hosting;
using LiveSplit.Options;
using LiveSplit.UI.Components;
using LiveSplit.Web.SRL;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace LiveSplit.Services;

/// <summary>
/// Fills Core's <see cref="ComponentManager.ComponentFactories"/>. Sources, later ones do not replace earlier ones:
///  1. <c>Components/</c> next to the executable (Core's own assembly scan; this is where the build copies components),
///  2. <c>Components/</c> in the user data directory (~/.local/share/LiveSplit/Components),
///  3. assemblies next to the executable that carry a [ComponentFactory] attribute (statically referenced components),
///  4. factories added with <see cref="Register"/>.
/// The key is the assembly file name (LiveSplit.Timer.dll), which is what .lsl files store as component path.
/// </summary>
public sealed class ComponentCatalog : ComponentManager
{
    private static readonly Dictionary<string, IComponentFactory> Registered = [];
    private static bool resolverInstalled;

    public static string UserComponentsDirectory => Path.Combine(Paths.DataDirectory, PATH_COMPONENTS);

    public static void Register(string fileName, IComponentFactory factory)
    {
        Registered[fileName] = factory;
        ComponentFactories?.TryAdd(fileName, factory);
    }

    public static void Initialize()
    {
        InstallResolver();

        var factories = new Dictionary<string, IComponentFactory>(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, IComponentFactory> pair in LoadAllFactories<IComponentFactory>())
        {
            factories.TryAdd(pair.Key, pair.Value);
        }

        ScanDirectory(UserComponentsDirectory, "*.dll", factories);

        foreach (string path in Directory.EnumerateFiles(Paths.ExecutableDirectory, "LiveSplit.*.dll"))
        {
            string name = Path.GetFileName(path);
            if (name is "LiveSplit.Core.dll" or "LiveSplit.Drawing.dll" || factories.ContainsKey(name))
            {
                continue;
            }

            IComponentFactory factory = LoadFactory<IComponentFactory>(path);
            if (factory != null)
            {
                factories[name] = factory;
            }
        }

        foreach (KeyValuePair<string, IComponentFactory> pair in Registered)
        {
            factories.TryAdd(pair.Key, pair.Value);
        }

        ComponentFactories = factories;
        Log.Info($"Loaded {factories.Count} component(s): {string.Join(", ", factories.Keys.OrderBy(x => x))}");
    }

    public static void InitializeRaceProviders()
    {
        IDictionary<string, IRaceProviderFactory> providers = LoadAllFactories<IRaceProviderFactory>();
        providers["SRL"] = new SRLFactory();
        RaceProviderFactories = providers;
    }

    private static void ScanDirectory(string directory, string pattern, Dictionary<string, IComponentFactory> factories)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string path in Directory.EnumerateFiles(directory, pattern))
        {
            string name = Path.GetFileName(path);
            if (factories.ContainsKey(name))
            {
                continue;
            }

            IComponentFactory factory = LoadFactory<IComponentFactory>(path);
            if (factory != null)
            {
                factories[name] = factory;
            }
        }
    }

    /// <summary>Components may ship private dependencies next to themselves; let the default context find them.</summary>
    private static void InstallResolver()
    {
        if (resolverInstalled)
        {
            return;
        }

        resolverInstalled = true;
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            foreach (string directory in new[] { Paths.ComponentsDirectory, UserComponentsDirectory })
            {
                string candidate = Path.Combine(directory, name.Name + ".dll");
                if (File.Exists(candidate))
                {
                    try
                    {
                        return AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                }
            }

            return null;
        };
    }
}
