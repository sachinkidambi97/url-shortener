using FluentAssertions;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests.Unit;

public sealed class ShortCodeGeneratorTests
{
    [Fact]
    public void Generate_DefaultLength_Returns7CharString()
    {
        var code = ShortCodeGenerator.Generate();
        code.Should().HaveLength(7);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void Generate_SpecifiedLength_ReturnsCorrectLength(int length)
    {
        var code = ShortCodeGenerator.Generate(length);
        code.Should().HaveLength(length);
    }

    [Fact]
    public void Generate_ReturnsOnlyBase62Characters()
    {
        const string base62 = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        for (int i = 0; i < 100; i++)
        {
            var code = ShortCodeGenerator.Generate();
            code.Should().MatchRegex("^[a-zA-Z0-9]+$", because: $"code '{code}' should only contain Base62 characters");
            foreach (char c in code)
            {
                base62.Should().Contain(c.ToString(), because: $"character '{c}' should be in Base62 alphabet");
            }
        }
    }

    [Fact]
    public void Generate_MultipleCalls_ProduceDifferentCodes()
    {
        var codes = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            codes.Add(ShortCodeGenerator.Generate());
        }
        // With 62^7 = 3.5T possible codes, collisions in 50 calls should be astronomically unlikely
        codes.Should().HaveCountGreaterThan(40, because: "crypto-random codes should rarely collide");
    }

    [Fact]
    public void Generate_ZeroLength_ThrowsArgumentOutOfRangeException()
    {
        var act = () => ShortCodeGenerator.Generate(0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Generate_NegativeLength_ThrowsArgumentOutOfRangeException()
    {
        var act = () => ShortCodeGenerator.Generate(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
