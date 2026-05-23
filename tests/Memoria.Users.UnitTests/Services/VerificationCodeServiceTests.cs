using FluentAssertions;

using Memoria.Users.Services;

namespace Memoria.Users.UnitTests.Services;

public sealed class VerificationCodeServiceTests
{
    private readonly VerificationCodeService _sut = new();

    [Fact]
    public void GenerateNumericCodeDefaultReturnsSixDigitString()
    {
        var code = _sut.GenerateNumericCode();

        code.Should().HaveLength(6);
        code.Should().MatchRegex("^[0-9]{6}$");
    }

    [Fact]
    public void GenerateNumericCodeReturnsDifferentValuesAcrossCalls()
    {
        var codes = Enumerable.Range(0, 50).Select(_ => _sut.GenerateNumericCode()).ToHashSet();

        codes.Should().HaveCountGreaterThan(1, "rng should not collapse to a single value");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void GenerateNumericCodeWithOutOfRangeLengthThrows(int length)
    {
        Action act = () => _sut.GenerateNumericCode(length);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GenerateLinkingTokenReturns32HexCharsWithoutDashes()
    {
        var token = _sut.GenerateLinkingToken();

        token.Should().HaveLength(32);
        token.Should().MatchRegex("^[0-9a-f]{32}$");
    }

    [Fact]
    public void HashThenVerifyRoundtripSucceeds()
    {
        const string plain = "123456";
        var hash = _sut.Hash(plain);

        _sut.Verify(plain, hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyWithWrongCodeReturnsFalse()
    {
        var hash = _sut.Hash("123456");

        _sut.Verify("654321", hash).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "hash")]
    [InlineData("", "hash")]
    [InlineData("code", null)]
    [InlineData("code", "")]
    public void VerifyWithNullOrEmptyArgumentsReturnsFalse(string? plain, string? hash)
    {
        _sut.Verify(plain!, hash!).Should().BeFalse();
    }

    [Fact]
    public void VerifyWithCorruptedHashReturnsFalseInsteadOfThrowing()
    {
        _sut.Verify("123456", "not-a-bcrypt-hash").Should().BeFalse();
    }
}
