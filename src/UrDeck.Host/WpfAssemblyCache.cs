// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Patrick Bigler

using System.Reflection;
using System.Runtime.Loader;
using UrDeck.Core.Diagnostics;

namespace UrDeck.Host;

/// <summary>
/// Works around WPF pinning unloadable plugins: MS.Internal.*.SafeSecurityHelper keeps a static
/// Assembly -> AssemblyName cache that never evicts, so any collectible plugin assembly WPF has seen stays
/// rooted and its AssemblyLoadContext can't unload on hot-reload. Found via gcroot on the LoaderAllocator.
/// This touches private WPF internals, so it is best-effort and fails soft.
/// </summary>
internal static class WpfAssemblyCache
{
    private static readonly (Assembly Assembly, string TypeName)[] Caches =
    {
        (typeof(System.Windows.Media.Brush).Assembly, "MS.Internal.PresentationCore.SafeSecurityHelper"),
        (typeof(System.Windows.DependencyObject).Assembly, "MS.Internal.WindowsBase.SafeSecurityHelper"),
    };

    /// <summary>Removes entries for assemblies from collectible load contexts. Call on the UI thread after plugin views are gone.</summary>
    public static void EvictCollectibleAssemblies()
    {
        foreach (var (assembly, typeName) in Caches)
        {
            try
            {
                var field = assembly.GetType(typeName)?.GetField("_assemblies", BindingFlags.NonPublic | BindingFlags.Static);
                if (field?.GetValue(null) is not Dictionary<object, AssemblyName> cache)
                    continue;

                lock (cache)
                {
                    foreach (object? key in cache.Keys.ToList())
                    {
                        if (key is Assembly a && AssemblyLoadContext.GetLoadContext(a)?.IsCollectible == true)
                            cache.Remove(key);
                    }
                }
            }
            catch (Exception ex)
            {
                UrDeckLog.Warn($"Could not purge WPF assembly cache {typeName}; old plugin versions may stay in memory: {ex.Message}");
            }
        }
    }
}
