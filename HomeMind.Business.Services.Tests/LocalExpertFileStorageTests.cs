using HomeMind.Business.Services.Expert;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HomeMind.Business.Services.Tests;

public sealed class LocalExpertFileStorageTests
{
    [Fact]
    public async Task MissingRootDoesNotPreventConstructionWhenStorageIsDisabled()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var storage = new LocalExpertFileStorage(configuration);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.WriteGeneratedAsync(1, 1, "file.txt", []));

        Assert.Contains("未启用", exception.Message);
    }
}
