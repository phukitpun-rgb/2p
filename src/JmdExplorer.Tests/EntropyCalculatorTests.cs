using JmdExplorer.Core.Services;
using Xunit;

namespace JmdExplorer.Tests;

public class EntropyCalculatorTests
{
    [Fact]
    public void AllZeroBytes_HaveZeroEntropy()
    {
        var data = new byte[4096]; // all zeros
        double entropy = EntropyCalculator.Compute(data);
        Assert.Equal(0d, entropy, precision: 6);
    }

    [Fact]
    public void SingleRepeatedValue_HaveZeroEntropy()
    {
        var data = new byte[1000];
        Array.Fill(data, (byte)0xAB);
        Assert.Equal(0d, EntropyCalculator.Compute(data), precision: 6);
    }

    [Fact]
    public void UniformAllByteValues_ApproachEightBits()
    {
        // Every byte value appears exactly once => maximum entropy of 8 bits.
        var data = new byte[256];
        for (int i = 0; i < 256; i++) data[i] = (byte)i;
        double entropy = EntropyCalculator.Compute(data);
        Assert.Equal(8d, entropy, precision: 6);
    }

    [Fact]
    public void TwoEquallyLikelyValues_GiveOneBit()
    {
        var data = new byte[1000];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 2);
        double entropy = EntropyCalculator.Compute(data);
        Assert.Equal(1d, entropy, precision: 3);
    }

    [Fact]
    public void EmptyData_IsZero()
    {
        Assert.Equal(0d, EntropyCalculator.Compute(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void StreamOverload_MatchesSpanOverload()
    {
        var data = new byte[2048];
        new Random(7).NextBytes(data);
        using var ms = new MemoryStream(data);
        double streamed = EntropyCalculator.Compute(ms, 0, data.Length);
        double span = EntropyCalculator.Compute(data);
        Assert.Equal(span, streamed, precision: 9);
    }
}
