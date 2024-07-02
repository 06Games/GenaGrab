using GeneaGrab.Core.Providers;

namespace GeneaGrab.Core.Tests.Providers.FR_AD79_86;

public class TestAD79_86
{
    private readonly AD79_86 instance = new();

    [Theory(DisplayName = "Check information retriever")]
    [ClassData(typeof(DataAD79_86))]
    public async Task CheckInfos(Data data)
    {
        var (registry, pageNumber) = await instance.Infos(new Uri(data.URL));
        Assert.Equal("AD79-86", registry.ProviderId);
        Assert.Equal(data.Id, registry.Id);
        Assert.Equal(data.Page, pageNumber);
        Assert.Equal(data.Cote, registry.CallNumber);
        Assert.Equal(data.From, registry.From);
        Assert.Equal(data.To, registry.To);

        var pos = new List<string> { data.Ville };
        if (data.Paroisse != null) pos.Add(data.Paroisse);
        Assert.Equal(pos, registry.Location);

        var types = registry.Types.ToArray();
        Assert.All(data.Types, type => Assert.Contains(type, types));
        Assert.Equal(data.Types.Length, types.Length);
    }
}
