using System.Reflection;
using System.Runtime.Loader;

namespace Winshell.TestSupport;

internal sealed class FolderLoadContext(string folder, params string[] names) : AssemblyLoadContext
{
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (!names.Contains(assemblyName.Name, StringComparer.Ordinal))
            return null;
        using var stream = File.OpenRead(Path.Combine(folder, assemblyName.Name + ".dll"));
        return LoadFromStream(stream);
    }
}
