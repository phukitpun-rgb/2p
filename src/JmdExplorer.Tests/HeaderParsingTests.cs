using JmdExplorer.Core.Models;
using JmdExplorer.Core.Services;
using JmdExplorer.Decoders.Profiles;
using JmdExplorer.Tools.SampleGenerator;
using Xunit;

namespace JmdExplorer.Tests;

public class HeaderParsingTests
{
    private static JmdFileContext ContextFor(byte[] data)
    {
        byte[] header = data.Take(4096).ToArray();
        return new JmdFileContext(
            "test.jmd", data.Length, header,
            () => new MemoryStream(data, writable: false),
            DateTime.UtcNow, DateTime.UtcNow);
    }

    [Fact]
    public void XenonProfile_RecognizesMarker_WithHighConfidence()
    {
        var data = SampleFileBuilder.BuildXenonSample();
        var profile = new XenonDataFormatV4sProfile();

        using var probe = new MemoryStream(data);
        using var reader = new BinaryReader(probe);
        Assert.True(profile.CanHandle(reader));

        using var analyze = new MemoryStream(data);
        var result = profile.Analyze(analyze);

        Assert.Equal("Xenon Data Format v4s", result.FormatName);
        Assert.Equal(Confidence.High, result.Confidence);
        Assert.False(result.DecoderAvailable);                       // honesty: no decoder
        Assert.Equal("Not implemented", result.DecoderStatus);
        Assert.Equal(DecodeStatus.StructureRecognized, result.Status);
        Assert.Contains("Xenon Data Format v4s", result.HeaderText);
    }

    [Fact]
    public void XenonProfile_DoesNotRecognizeRandomData()
    {
        var data = SampleFileBuilder.BuildUnknownSample();
        var profile = new XenonDataFormatV4sProfile();
        using var probe = new MemoryStream(data);
        using var reader = new BinaryReader(probe);
        Assert.False(profile.CanHandle(reader));
    }

    [Fact]
    public void UnknownProfile_AlwaysHandles_ButWithNoConfidence()
    {
        var data = SampleFileBuilder.BuildUnknownSample();
        var profile = new UnknownBinaryProfile();
        using var probe = new MemoryStream(data);
        using var reader = new BinaryReader(probe);
        Assert.True(profile.CanHandle(reader));

        using var analyze = new MemoryStream(data);
        var result = profile.Analyze(analyze);
        Assert.Equal(Confidence.None, result.Confidence);
        Assert.False(result.DecoderAvailable);
    }

    [Fact]
    public void FormatDetector_PrefersXenonOverUnknown()
    {
        var detector = new FormatDetector(new IFormatProfileArray());
        var data = SampleFileBuilder.BuildXenonSample();
        var result = detector.Detect(ContextFor(data));
        Assert.Equal("Xenon Data Format v4s", result.FormatName);
        Assert.Equal(Confidence.High, result.Confidence);
    }

    [Fact]
    public void FormatDetector_FallsBackToUnknown()
    {
        var detector = new FormatDetector(new IFormatProfileArray());
        var data = SampleFileBuilder.BuildUnknownSample();
        var result = detector.Detect(ContextFor(data));
        Assert.Equal("Unknown binary", result.FormatName);
    }

    /// <summary>Helper enumerable mirroring the real DI registration order.</summary>
    private sealed class IFormatProfileArray : List<Core.Abstractions.IFormatProfile>
    {
        public IFormatProfileArray()
        {
            Add(new XenonDataFormatV4sProfile());
            Add(new UnknownBinaryProfile());
        }
    }
}
