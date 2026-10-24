using System.Buffers.Binary;

namespace Modelado.Architecture.Tests;

/// <summary>
/// MOD7: the app icon is a real .ico holding every size the shell asks for, and both the exe and
/// the window wear it. Read from the files, as the rest of this project's architecture tests are.
/// </summary>
public sealed class IconTests
{
    private static readonly string App = Path.Combine(Repository.Root, "src", "Modelado.App");

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public void TheIconHoldsEverySizeTheShellAsksFor()
    {
        byte[] ico = File.ReadAllBytes(Path.Combine(App, "Modelado.ico"));
        Assert.Equal(0, BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(0)));
        Assert.Equal(1, BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(2)));

        int count = BinaryPrimitives.ReadUInt16LittleEndian(ico.AsSpan(4));
        List<int> sizes = [];
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> entry = ico.AsSpan(6 + (16 * i), 16);
            sizes.Add(entry[0] == 0 ? 256 : entry[0]);
            int length = BinaryPrimitives.ReadInt32LittleEndian(entry[8..]);
            int offset = BinaryPrimitives.ReadInt32LittleEndian(entry[12..]);
            Assert.True(offset + length <= ico.Length, $"entry {i} runs past the end of the file");
            Assert.True(ico.AsSpan(offset, PngSignature.Length).SequenceEqual(PngSignature), $"entry {i} is not a PNG");
        }

        Assert.Superset(new HashSet<int> { 16, 24, 32, 48, 256 }, sizes.ToHashSet());
    }

    [Fact]
    public void TheExeAndTheWindowWearIt()
    {
        string project = File.ReadAllText(Path.Combine(App, "Modelado.App.csproj"));
        Assert.Contains("<ApplicationIcon>Modelado.ico</ApplicationIcon>", project, StringComparison.Ordinal);
        Assert.Contains("<Resource Include=\"Modelado.ico\" />", project, StringComparison.Ordinal);

        string window = File.ReadAllText(Path.Combine(App, "MainWindow.xaml"));
        Assert.Contains("Icon=\"pack://application:,,,/Modelado.ico\"", window, StringComparison.Ordinal);
    }
}
