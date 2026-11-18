namespace Winshell.Services.Tests;

[TestClass]
public sealed class ShellImageTests
{
    [TestMethod]
    public void A_program_has_an_icon_at_least_the_size_asked_for()
    {
        var image = ShellImage.Load(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), 32);

        Assert.IsNotNull(image);
        Assert.IsTrue(image is { Width: >= 32, Height: >= 32 }, $"{image.Width}x{image.Height}");
        Assert.IsTrue(image.Premultiplied);
        var alpha = image.Bgra.ToArray().Where((_, i) => i % 4 == 3);
        Assert.Contains(a => a > 0, alpha);
    }

    [TestMethod]
    public void A_path_with_no_item_has_no_icon()
    {
        Assert.IsNull(ShellImage.Load(Path.Combine(TempFolder.NewPath(), "missing.exe"), 32));
    }
}
