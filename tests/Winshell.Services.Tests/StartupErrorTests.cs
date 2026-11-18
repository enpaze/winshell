using Winshell.Native;

namespace Winshell.Services.Tests;

[TestClass]
public class StartupErrorTests
{
    [TestMethod]
    [DataRow(0, (int)StreamTarget.Other, true)]
    [DataRow(0, (int)StreamTarget.File, false)]
    [DataRow(0, (int)StreamTarget.Pipe, false)]
    [DataRow(1, (int)StreamTarget.Other, false)]
    public void A_dialog_is_only_for_an_error_nobody_else_can_read(int window, int stderr, bool dialog)
    {
        Assert.AreEqual(dialog, StartupError.NeedsDialog(window, (StreamTarget)stderr));
    }

    [TestMethod]
    public void A_path_opens_that_folder_and_a_missing_root_opens_nothing()
    {
        var root = TempFolder.NewPath();
        var path = Path.Combine(root, "MyShell.exe");
        Directory.CreateDirectory(root);
        try
        {
            Assert.AreEqual(root, StartupError.FolderFor(path, Path.Combine(root, "absent")));
            Assert.AreEqual(root, StartupError.FolderFor(null, root));
            Assert.IsNull(StartupError.FolderFor(null, Path.Combine(root, "absent")));
            Assert.IsNull(StartupError.FolderFor(Path.Combine(root, "gone", "MyShell.exe"), root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
