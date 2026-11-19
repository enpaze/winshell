using System.Reflection;
using System.Reflection.Emit;

namespace Winshell.Services.Tests;

[TestClass]
public sealed class ShellPropertiesTests
{
    [TestMethod]
    public void A_program_s_metadata_gives_its_shell_name_and_folders()
    {
        var program = Program(("ShellName", "mine"), ("ShellDataDir", "data"), ("ShellStateDir", @"$BASE\state"),
            ("ShellCacheDir", @"%TEMP%\cache"), ("Other", "kept out"));

        Assert.AreEqual(new ShellProperties("mine", "data", @"$BASE\state", @"%TEMP%\cache"), ShellProperties.Of(program));
    }

    [TestMethod]
    public void A_program_without_them_or_with_empty_ones_sets_none()
    {
        Assert.AreEqual(new ShellProperties(), ShellProperties.Of(Program(("ShellName", ""), ("ShellDataDir", null))));
        Assert.AreEqual(new ShellProperties(), ShellProperties.Of(null));
    }

    private static AssemblyBuilder Program(params (string Key, string? Value)[] metadata)
    {
        var constructor = typeof(AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)])!;
        return AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Program" + Guid.NewGuid().ToString("N")),
            AssemblyBuilderAccess.Run,
            metadata.Select(m => new CustomAttributeBuilder(constructor, [m.Key, m.Value])));
    }
}
