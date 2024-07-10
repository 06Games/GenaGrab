using GeneaGrab.Core.Helpers;
using GeneaGrab.Core.Providers;
using Xunit.Abstractions;

namespace GeneaGrab.Core.Tests.Providers.FR_AMNice;

public class TestAMNice(ITestOutputHelper output)
{
    private readonly Nice instance = new();

    [Theory(DisplayName = "Check information retriever")]
    [ClassData(typeof(DataAMNice))]
    public async Task CheckInfos(Data data)
    {
        var (registry, pageNumber) = await instance.Infos(new Uri(data.URL));
        output.WriteLine(await Json.StringifyAsync(registry));
        Assert.Equal("AMNice", registry.ProviderId);
        Assert.Equal(data.Id, registry.Id);
        Assert.Equal(data.Page, pageNumber);
        Assert.Equal(data.Cote, registry.CallNumber);
        Assert.Equal(data.Auteur, registry.Author);
        Assert.Equal(data.From, registry.From);
        Assert.Equal(data.To, registry.To);

        var pos = data.DetailPosition.Append(data.Ville);
        if (data.Rue != null) pos = pos.Append(data.Rue);
        Assert.Equal(pos, registry.Location);

        var types = registry.Types.ToArray();
        Assert.True(data.Types.Length == types.Length, $"{string.Join(", ", types)}\nExpected: {data.Types}");
        Assert.All(data.Types, type => Assert.Contains(type, types));
    }
}
