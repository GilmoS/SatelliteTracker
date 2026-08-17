using SatelliteTracker.Database.Security;
using Xunit;

namespace SatelliteTracker.Tests.Security;

public class ApiKeyHasherTests
{
    [Fact]
    public void Hash_SameInput_ProducesSameHash()
    {
        var hash1 = ApiKeyHasher.Hash("same-raw-key");
        var hash2 = ApiKeyHasher.Hash("same-raw-key");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Hash_DifferentInput_ProducesDifferentHash()
    {
        var hash1 = ApiKeyHasher.Hash("raw-key-one");
        var hash2 = ApiKeyHasher.Hash("raw-key-two");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Hash_ReturnsLowercase64CharHex()
    {
        var hash = ApiKeyHasher.Hash("some-key");

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, hash.ToLowerInvariant());
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }
}
