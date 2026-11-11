using System.Runtime.CompilerServices;

namespace Winshell.TestSupport;

/// <summary>Loads <see cref="ParentProcessWatch"/> so its module initializer runs in this test program.</summary>
internal static class TestSupportLoad
{
    [ModuleInitializer]
    internal static void Load() => _ = typeof(ParentProcessWatch);
}
