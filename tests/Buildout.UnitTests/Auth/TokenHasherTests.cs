using Buildout.Mcp.Auth;
using Xunit;

namespace Buildout.UnitTests.Auth;

public sealed class TokenHasherTests
{
    [Fact]
    public void Hash_Returns64CharLowercaseHex()
    {
        var token = "mcp_test_token_123";
        var hash = TokenHasher.Hash(token);

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void Hash_SameToken_SameHash()
    {
        var token = "mcp_test_token_123";
        var hash1 = TokenHasher.Hash(token);
        var hash2 = TokenHasher.Hash(token);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Hash_DifferentTokens_DifferentHashes()
    {
        var token1 = "mcp_test_token_123";
        var token2 = "mcp_test_token_124";

        var hash1 = TokenHasher.Hash(token1);
        var hash2 = TokenHasher.Hash(token2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Hash_SmallDifference_LargeHashDifference()
    {
        var token1 = "token_abc";
        var token2 = "token_abd";

        var hash1 = TokenHasher.Hash(token1);
        var hash2 = TokenHasher.Hash(token2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Verify_MatchingToken_ReturnsTrue()
    {
        var token = "mcp_test_token_123";
        var hash = TokenHasher.Hash(token);

        var result = TokenHasher.Verify(token, hash);

        Assert.True(result);
    }

    [Fact]
    public void Verify_WrongToken_ReturnsFalse()
    {
        var token1 = "mcp_test_token_123";
        var token2 = "mcp_test_token_124";
        var hash = TokenHasher.Hash(token1);

        var result = TokenHasher.Verify(token2, hash);

        Assert.False(result);
    }

    [Fact]
    public void Verify_EmptyToken_DoesNotMatch()
    {
        var token = "mcp_test_token_123";
        var hash = TokenHasher.Hash(token);

        var result = TokenHasher.Verify("", hash);

        Assert.False(result);
    }

    [Fact]
    public void Verify_TimingSafeComparison()
    {
        var token = "mcp_test_token_123";
        var hash = TokenHasher.Hash(token);
        var wrongHash = "a" + hash[1..];

        var result = TokenHasher.Verify(token, wrongHash);

        Assert.False(result);
    }

    [Fact]
    public void Hash_SpecialCharacters_HandlesCorrectly()
    {
        var token = "mcp_!@#$%^&*()_+-={}[]|\\:\";'<>?,./`~";
        var hash = TokenHasher.Hash(token);

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);

        var result = TokenHasher.Verify(token, hash);
        Assert.True(result);
    }

    [Fact]
    public void Hash_LongToken_HandlesCorrectly()
    {
        var token = new string('x', 1000);
        var hash = TokenHasher.Hash(token);

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);

        var result = TokenHasher.Verify(token, hash);
        Assert.True(result);
    }

    [Fact]
    public void Hash_UuidToken_Deterministic()
    {
        var token = $"mcp_{Guid.NewGuid():N}";
        var hash1 = TokenHasher.Hash(token);
        var hash2 = TokenHasher.Hash(token);

        Assert.Equal(hash1, hash2);
    }
}