using System;
using System.IO;
using System.Text;
using CodeImp.DoomBuilder.IO;
using Xunit;

namespace DoomBuilder.Core.Tests;

public class WadTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), "udb-test-" + Guid.NewGuid().ToString("N") + ".wad");

    public void Dispose() { if (File.Exists(path)) File.Delete(path); }

    [Fact]
    public void Lumps_written_to_a_new_wad_can_be_read_back()
    {
        byte[] map01 = Array.Empty<byte>();
        byte[] things = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        using (var wad = new WAD(path))
        {
            wad.Insert("MAP01", wad.Lumps.Count, map01.Length);
            Lump l = wad.Insert("THINGS", wad.Lumps.Count, things.Length);
            l.Stream.Write(things, 0, things.Length);
            wad.WriteHeaders();
        }

        using (var wad = new WAD(path, true))
        {
            Assert.Equal(2, wad.Lumps.Count);
            Assert.Equal("MAP01", wad.Lumps[0].Name);
            Assert.Equal("THINGS", wad.Lumps[1].Name);

            Lump l = wad.FindLump("THINGS");
            Assert.NotNull(l);
            Assert.Equal(things.Length, l.Length);
            var read = new byte[things.Length];
            l.Stream.Seek(0, SeekOrigin.Begin);
            l.Stream.ReadExactly(read, 0, read.Length);
            Assert.Equal(things, read);
        }
    }

    [Fact]
    public void Wad_header_starts_with_a_wad_magic_and_lump_count()
    {
        using (var wad = new WAD(path))
        {
            wad.Insert("A", 0, 0);
            wad.Insert("B", 1, 0);
            wad.WriteHeaders();
        }
        byte[] header = File.ReadAllBytes(path);
        string magic = Encoding.ASCII.GetString(header, 0, 4);
        Assert.True(magic == "PWAD" || magic == "IWAD");
        Assert.Equal(2, BitConverter.ToInt32(header, 4));
    }
}
