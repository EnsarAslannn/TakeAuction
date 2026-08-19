using TakeAuction.Api.Common.Security;

namespace TakeAuction.Api.UnitTests.Common.Security;

public sealed class RefreshTokenGeneratorTests
{
    private readonly RefreshTokenGenerator _generator = new();

    [Fact]
    public void Every_token_is_different()
    {
        var values = Enumerable
            .Range(0, 200)
            .Select(_ => _generator.Generate().Value)
            .ToList();

        Assert.Equal(values.Count, values.Distinct().Count());
    }

    [Fact]
    public void A_token_carries_enough_entropy_to_resist_guessing()
    {
        var token = _generator.Generate();

        Assert.Equal(RefreshTokenGenerator.TokenLengthInCharacters, token.Value.Length);
        Assert.All(token.Value, character => Assert.True(Uri.IsHexDigit(character)));
    }

    [Fact]
    public void Hashing_is_deterministic()
    {
        var token = _generator.Generate();

        Assert.Equal(token.Hash, _generator.Hash(token.Value));
        Assert.Equal(_generator.Hash("abc"), _generator.Hash("abc"));
    }

    [Fact]
    public void Different_tokens_hash_differently()
    {
        var first = _generator.Generate();
        var second = _generator.Generate();

        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public void The_stored_hash_is_never_the_token_itself()
    {
        var token = _generator.Generate();

        Assert.NotEqual(token.Value, token.Hash);
        Assert.DoesNotContain(token.Value, token.Hash, StringComparison.Ordinal);
    }
}
