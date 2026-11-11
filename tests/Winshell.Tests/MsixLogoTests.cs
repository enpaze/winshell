using System.Buffers.Binary;
using System.Globalization;
using System.Xml.Linq;

namespace Winshell.Tests;

public class MsixLogoTests
{
    // Windows looks up these scale qualifiers next to the manifest's logos.
    private static readonly int[] Scales = [125, 150, 200];

    [Fact]
    public void The_package_logos_include_scale_and_unplated_sizes()
    {
        var assets = Path.Combine(Repo.Root, "packaging", "msix", "Assets");
        var files = Directory.GetFiles(assets, "*.png").Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        Assert.NotEmpty(files);
        foreach (var name in files)
            AssertSize(assets, name!, Side(assets, name!));

        foreach (var logo in Logos(Path.Combine(Repo.Root, "packaging", "msix", "AppxManifest.xml")))
        {
            Assert.Contains(logo, files);
            var bare = Path.GetFileNameWithoutExtension(logo);
            foreach (var scale in Scales)
                Assert.Contains($"{bare}.scale-{scale}.png", files);
        }

        Assert.Contains(files, name => name!.Contains(".targetsize-", StringComparison.Ordinal));
    }

    /// <summary>The side a file name states, or the base image's side for a Store logo.</summary>
    private static int Side(string assets, string name)
    {
        var target = name.IndexOf(".targetsize-", StringComparison.Ordinal);
        if (target >= 0)
            return Number(name, target + ".targetsize-".Length, '_');

        var scale = 100;
        var stem = name;
        var qualifier = name.IndexOf(".scale-", StringComparison.Ordinal);
        if (qualifier >= 0)
        {
            scale = Number(name, qualifier + ".scale-".Length, '.');
            stem = string.Concat(name.AsSpan(0, qualifier), ".png");
        }

        var square = SquareSide(stem);
        var basis = square > 0 ? square : Width(Path.Combine(assets, stem));
        return (int)Math.Round(basis * (scale / 100d), MidpointRounding.AwayFromZero);
    }

    private static int SquareSide(string name)
    {
        const string head = "Square";
        if (!name.StartsWith(head, StringComparison.Ordinal))
            return 0;
        var mark = name.IndexOf('x', StringComparison.Ordinal);
        var logo = name.IndexOf("Logo.png", StringComparison.Ordinal);
        if (mark < head.Length || logo < 0)
            return 0;
        var left = name[head.Length..mark];
        var right = name[(mark + 1)..logo];
        return left == right && int.TryParse(left, CultureInfo.InvariantCulture, out var side) ? side : 0;
    }

    private static int Number(string name, int start, char end)
    {
        var digits = name[start..];
        var stop = digits.IndexOf(end);
        return int.Parse(stop < 0 ? digits : digits[..stop], CultureInfo.InvariantCulture);
    }

    private static void AssertSize(string directory, string name, int size)
    {
        var bytes = File.ReadAllBytes(Path.Combine(directory, name));
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, bytes[..8]);
        Assert.Equal(size, Dimension(bytes, 16));
        Assert.Equal(size, Dimension(bytes, 20));
    }

    private static int Width(string path) => Dimension(File.ReadAllBytes(path), 16);

    private static int Dimension(byte[] png, int offset) => BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));

    private static IEnumerable<string> Logos(string manifestPath)
    {
        var manifest = XDocument.Load(manifestPath);
        XNamespace package = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
        XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";
        yield return Path.GetFileName(manifest.Descendants(package + "Logo").Single().Value);
        var visual = manifest.Descendants(uap + "VisualElements").Single();
        yield return Path.GetFileName(visual.Attribute("Square150x150Logo")!.Value);
        yield return Path.GetFileName(visual.Attribute("Square44x44Logo")!.Value);
    }
}
