using System.Text;
using JmdExplorer.Core.Services;
using Xunit;

namespace JmdExplorer.Tests;

public class J2mConfigExtractorTests
{
    private static void WriteStr(List<byte> b, string s)
    {
        var u = Encoding.Unicode.GetBytes(s);
        b.AddRange(BitConverter.GetBytes((uint)s.Length));
        b.AddRange(u);
    }

    /// <summary>Builds a minimal J2m "param" config block: param + pad + count + key/value pairs.</summary>
    private static byte[] BuildBlock()
    {
        var b = new List<byte>();
        b.AddRange(new byte[8]); // leading padding
        WriteStr(b, "param");      // node marker the extractor searches for
        b.AddRange(new byte[8]);   // 8 pad bytes
        b.AddRange(BitConverter.GetBytes((uint)2)); // child count (advisory)
        WriteStr(b, "BD_Mass"); WriteStr(b, "1300"); b.AddRange(new byte[8]);
        WriteStr(b, "emblem"); WriteStr(b, "TestCar"); b.AddRange(new byte[8]);
        // a stop key ends the block
        WriteStr(b, "mesh");
        return b.ToArray();
    }

    [Fact]
    public void Extract_ReadsKeyValuePairs_AndEmblemLabel()
    {
        var configs = J2mConfigExtractor.Extract(BuildBlock());
        Assert.Single(configs);
        var c = configs[0];
        Assert.Equal("TestCar", c.Label);
        Assert.Contains("<BD_Mass>1300</BD_Mass>", c.Xml);
        Assert.Contains("<emblem>TestCar</emblem>", c.Xml);
        Assert.StartsWith("<?xml", c.Xml);
        Assert.Contains("</car>", c.Xml);
    }

    [Fact]
    public void Extract_NoParam_ReturnsEmpty()
    {
        var configs = J2mConfigExtractor.Extract(new byte[256]);
        Assert.Empty(configs);
    }

    [Fact]
    public void Extract_TabWrappedValues_AreTrimmed()
    {
        var b = new List<byte>();
        b.AddRange(new byte[8]);
        WriteStr(b, "param");
        b.AddRange(new byte[8]);
        b.AddRange(BitConverter.GetBytes((uint)1));
        WriteStr(b, "opEG_RedRPM"); WriteStr(b, "\t130\t"); b.AddRange(new byte[8]);
        WriteStr(b, "mesh");
        var configs = J2mConfigExtractor.Extract(b.ToArray());
        Assert.Single(configs);
        Assert.Contains("<opEG_RedRPM>130</opEG_RedRPM>", configs[0].Xml);
    }
}
