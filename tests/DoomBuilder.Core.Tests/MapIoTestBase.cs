using System;
using System.IO;
using System.Text;
using CodeImp.DoomBuilder;
using CodeImp.DoomBuilder.Config;
using CodeImp.DoomBuilder.IO;
using CodeImp.DoomBuilder.Map;
using Xunit;

namespace DoomBuilder.Core.Tests;

/// <summary>
/// Shared setup for tests that read maps without any UI: headless General, a throw-away WAD and a game configuration.
/// </summary>
[Collection("General static state")]
public abstract class MapIoTestBase : IDisposable
{
    protected readonly string WadPath = Path.Combine(Path.GetTempPath(), "udb-map-" + Guid.NewGuid().ToString("N") + ".wad");

    protected MapIoTestBase() => General.InitializeHeadless(TestAssets.DefaultSettings, TestAssets.Compilers);

    public void Dispose()
    {
        General.ShutdownHeadless();
        if (File.Exists(WadPath)) File.Delete(WadPath);
    }

    /// <summary>Fails with the logged messages, which is far more useful than a bare count.</summary>
    protected static void AssertNoErrors()
    {
        var messages = new System.Collections.Generic.List<string>();
        foreach (var e in General.ErrorLogger.GetErrors(0)) messages.Add(e.Type + ": " + e.Description);
        Assert.True(messages.Count == 0, Environment.NewLine + string.Join(Environment.NewLine, messages));
    }

    protected static byte[] Bytes(Action<BinaryWriter> write)
    {
        var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.ASCII, true)) write(w);
        return ms.ToArray();
    }

    protected static void Name8(BinaryWriter w, string name)
    {
        var b = new byte[8];
        Encoding.ASCII.GetBytes(name, 0, name.Length, b, 0);
        w.Write(b);
    }

    protected static void AddLump(WAD wad, string name, byte[] data)
    {
        Lump l = wad.Insert(name, wad.Lumps.Count, data.Length);
        l.Stream.Write(data, 0, data.Length);
    }

    protected static void AddLump(WAD wad, string name, string text) => AddLump(wad, name, Encoding.ASCII.GetBytes(text));

    /// <summary>Reads MAP01 from the test WAD with the given IO class and game configuration (file name in assets/Common/Configurations).</summary>
    /// <summary>Makes the headless map manager behave as if a map for this game configuration were open.</summary>
    protected static void UseGameConfig(string gameConfigFile)
    {
        string cfgpath = Path.Combine(TestAssets.Common, "Configurations", gameConfigFile);
        var gamecfg = new CodeImp.DoomBuilder.IO.Configuration(cfgpath, true);
        Assert.False(gamecfg.ErrorResult, gamecfg.ErrorDescription);
        General.Map.SetHeadlessGameConfiguration(new ConfigurationInfo(gamecfg, cfgpath));
    }

    internal MapSet ReadMap01(Func<WAD, MapSetIO> createIo, string gameConfigFile)
    {
        using var wad = new WAD(WadPath, true);
        MapSetIO io = createIo(wad);
        General.Map.SetHeadlessFormatInterface(io);
        UseGameConfig(gameConfigFile);

        var mapset = new MapSet();
        mapset.BeginAddRemove();               // the map manager always reads inside an add/remove block
        MapSet map = io.Read(mapset, "MAP01");
        map.EndAddRemove();
        return map;
    }
}
