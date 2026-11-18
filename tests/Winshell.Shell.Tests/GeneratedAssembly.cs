using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Winshell.ShellTests;

internal static class GeneratedAssembly
{
    internal static string Write(string folder, string name, AssemblyName[] references,
        params (string Name, AssemblyName Assembly, string BaseType)[] types)
    {
        var metadata = new MetadataBuilder();
        metadata.AddModule(0, metadata.GetOrAddString(name + ".dll"), metadata.GetOrAddGuid(Guid.NewGuid()), default,
            default);
        metadata.AddAssembly(metadata.GetOrAddString(name), new Version(1, 0, 0, 0), default, default, default,
            AssemblyHashAlgorithm.Sha1);
        var handles = new Dictionary<string, AssemblyReferenceHandle>(StringComparer.Ordinal);
        foreach (var reference in references.Concat(types.Select(type => type.Assembly)))
            if (!handles.ContainsKey(reference.Name!))
                handles[reference.Name!] = metadata.AddAssemblyReference(metadata.GetOrAddString(reference.Name!),
                    reference.Version ?? new Version(0, 0, 0, 0), default, default, default, default);
        metadata.AddTypeDefinition(default, default, metadata.GetOrAddString("<Module>"), default,
            MetadataTokens.FieldDefinitionHandle(1), MetadataTokens.MethodDefinitionHandle(1));
        foreach (var (type, assembly, baseType) in types)
        {
            var dot = baseType.LastIndexOf('.');
            var parent = metadata.AddTypeReference(handles[assembly.Name!],
                metadata.GetOrAddString(baseType[..dot]), metadata.GetOrAddString(baseType[(dot + 1)..]));
            metadata.AddTypeDefinition(TypeAttributes.Public | TypeAttributes.Class, metadata.GetOrAddString(name),
                metadata.GetOrAddString(type), parent, MetadataTokens.FieldDefinitionHandle(1),
                MetadataTokens.MethodDefinitionHandle(1));
        }

        var image = new BlobBuilder();
        new ManagedPEBuilder(PEHeaderBuilder.CreateLibraryHeader(), new MetadataRootBuilder(metadata), new BlobBuilder())
            .Serialize(image);
        var path = Path.Combine(folder, name + ".dll");
        using var file = File.Create(path);
        image.WriteContentTo(file);
        return path;
    }
}
