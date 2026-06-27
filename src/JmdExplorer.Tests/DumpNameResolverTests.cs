using System.Text;
using JmdExplorer.Core.Services;
using Xunit;

namespace JmdExplorer.Tests;

public class DumpNameResolverTests
{
    [Fact]
    public void ResolveDdsNames_MapsOffsetToRealName()
    {
        // --- jmd: a DDS magic at offset 0x100 ---
        var jmd = new byte[0x200];
        const int ddsOff = 0x100;
        jmd[ddsOff] = (byte)'D'; jmd[ddsOff + 1] = (byte)'D'; jmd[ddsOff + 2] = (byte)'S'; jmd[ddsOff + 3] = (byte)' ';

        // --- dump: entry struct (ext, hash@+8, name@+16) + index record (hash, offset@+4) ---
        uint hash = 0xABCD1234;
        var dump = new List<byte>();
        dump.AddRange(Encoding.ASCII.GetBytes("dds\0"));      // ext marker at +0
        dump.AddRange(new byte[4]);                            // pad (+4)
        dump.AddRange(BitConverter.GetBytes(hash));           // hash (+8)
        dump.AddRange(BitConverter.GetBytes((uint)1520));     // size (+12)
        dump.AddRange(Encoding.Unicode.GetBytes("body_tex")); // name (+16)
        dump.AddRange(new byte[] { 0, 0 });                   // name terminator
        dump.AddRange(new byte[32]);                           // gap
        // index record: hash immediately followed by the DDS offset
        dump.AddRange(BitConverter.GetBytes(hash));
        dump.AddRange(BitConverter.GetBytes((uint)ddsOff));

        var map = DumpNameResolver.ResolveDdsNames(jmd, dump.ToArray());

        Assert.True(map.ContainsKey(ddsOff));
        Assert.Equal("body_tex", map[ddsOff]);
    }

    [Fact]
    public void ResolveDdsNames_NoMatch_ReturnsEmpty()
    {
        var jmd = new byte[0x200];
        jmd[0x100] = (byte)'D'; jmd[0x101] = (byte)'D'; jmd[0x102] = (byte)'S'; jmd[0x103] = (byte)' ';
        // dump with no matching index/entry structures
        var map = DumpNameResolver.ResolveDdsNames(jmd, new byte[1024]);
        Assert.Empty(map);
    }
}
