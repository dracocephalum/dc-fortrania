using System.Reflection;
using System.Runtime.Loader;

namespace Dracocephalum.Fortrania.Plugins;

/// <summary>
/// Makes the plugins of an assembly named by path: the assembly in a load
/// context of its own, and of it every public type that implements
/// <see cref="ITranslationPlugin"/> and has a constructor that takes nothing.
/// </summary>
/// <remarks>
/// An assembly's own dependencies are found as its <c>.deps.json</c> says,
/// so that two plugins may each bring their own version of one. The
/// assembly that defines <see cref="ITranslationPlugin"/> is the
/// translator's, whatever copy lies beside a plugin: the plugin and the
/// translator then mean the same type by it.
/// </remarks>
internal static class PluginLoader
{
    // The assembly a plugin and the translator share.
    private static readonly string? Shared = typeof(ITranslationPlugin).Assembly.GetName().Name;

    /// <summary>Makes the plugins of an assembly, in the order of its types.</summary>
    /// <param name="path">The assembly, by full path.</param>
    /// <param name="plugins">What the plugins are added to.</param>
    /// <returns>Nothing when they were made, and what is wrong where the assembly is not there, cannot be loaded, or holds none.</returns>
    public static string? Load(string path, List<ITranslationPlugin> plugins)
    {
        if (!File.Exists(path))
        {
            return $"There is no plugin assembly {path}.";
        }

        try
        {
            var assembly = new PluginLoadContext(path).LoadFromAssemblyPath(path);
            var found = assembly.GetExportedTypes()
                .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(ITranslationPlugin).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) is not null)
                .Select(Activator.CreateInstance)
                .OfType<ITranslationPlugin>()
                .ToList();
            if (found.Count == 0)
            {
                return $"{path} holds no public type that implements {nameof(ITranslationPlugin)} and has a constructor that takes nothing.";
            }

            plugins.AddRange(found);
            return null;
        }
        catch (Exception exception) when (exception is BadImageFormatException or FileLoadException or FileNotFoundException or ReflectionTypeLoadException or TargetInvocationException or TypeLoadException)
        {
            return $"The plugins of {path} cannot be made: {(exception as TargetInvocationException)?.InnerException?.Message ?? exception.Message}";
        }
    }

    private sealed class PluginLoadContext(string path) : AssemblyLoadContext(Path.GetFileNameWithoutExtension(path))
    {
        private readonly AssemblyDependencyResolver _resolver = new(path);

        protected override Assembly? Load(AssemblyName assemblyName) =>
            assemblyName.Name == Shared
                ? null
                : _resolver.ResolveAssemblyToPath(assemblyName) is { } resolved ? LoadFromAssemblyPath(resolved) : null;

        protected override nint LoadUnmanagedDll(string unmanagedDllName) =>
            _resolver.ResolveUnmanagedDllToPath(unmanagedDllName) is { } resolved ? LoadUnmanagedDllFromPath(resolved) : 0;
    }
}
