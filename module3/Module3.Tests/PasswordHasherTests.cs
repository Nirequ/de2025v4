using Module3.Domain.Services;

namespace Module3.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ProducesAlgorithmPrefix()
    {
        var hash = PasswordHasher.Hash("S3cret!");
        Assert.StartsWith("pbkdf2_sha256$", hash);
        Assert.Equal(4, hash.Split('$').Length);
    }

    [Fact]
    public void Hash_IsRandomisedBySalt()
    {
        var h1 = PasswordHasher.Hash("S3cret!");
        var h2 = PasswordHasher.Hash("S3cret!");
        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void Verify_AcceptsCorrectPassword()
    {
        var hash = PasswordHasher.Hash("S3cret!");
        Assert.True(PasswordHasher.Verify("S3cret!", hash));
    }

    [Fact]
    public void Verify_RejectsWrongPassword()
    {
        var hash = PasswordHasher.Hash("S3cret!");
        Assert.False(PasswordHasher.Verify("wrong", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not_a_hash")]
    [InlineData("pbkdf2_sha256$abc$xx$yy")]
    public void Verify_RejectsMalformedHash(string stored)
    {
        Assert.False(PasswordHasher.Verify("anything", stored));
    }
}
