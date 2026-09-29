using System.Reflection;
using System.Runtime.Loader;

namespace Widgy.Core.Plugin;

/// <summary>
/// Collectible load context for a single plugin. Shared contract assemblies (Widgy.Core, SkiaSharp,
/// framework) resolve from the default context so type identity holds; only the plugin's own
/// private dependencies are loaded here.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _originalDirectory;

    public PluginLoadContext(string shadowPluginPath, string originalDirectory)
        : base($"plugin:{Path.GetFileName(shadowPluginPath)}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(shadowPluginPath);
        _originalDirectory = originalDirectory;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Defer to the default context whenever it can supply the assembly.
        try
        {
            return Default.LoadFromAssemblyName(assemblyName);
        }
        catch (Exception ex) when (ex is FileNotFoundException or FileLoadException or BadImageFormatException)
        {
            // Not a shared assembly; fall through to the plugin's private dependencies.
        }

        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path != null)
            return LoadFromAssemblyPath(path);

        // Private dependency dropped next to the plugin: read it from the original folder into memory
        // so the file isn't locked.
        string sibling = Path.Combine(_originalDirectory, assemblyName.Name + ".dll");
        if (File.Exists(sibling))
        {
            using var stream = new MemoryStream(File.ReadAllBytes(sibling));
            return LoadFromStream(stream);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path != null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
    }
}
