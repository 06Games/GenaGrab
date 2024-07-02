using GeneaGrab.Core.Helpers;
using GeneaGrab.Core.Models;
using GeneaGrab.Core.Providers;
using Xunit.Abstractions;

namespace GeneaGrab.Core.Tests.Providers.FR_AD06;

public class TestAD06(ITestOutputHelper output)
{
    private readonly AD06 instance = new(new HttpClient { Timeout = TimeSpan.FromSeconds(15) });
    private static int timeoutCount;

    [SkippableTheory(DisplayName = "Check information retriever")]
    [ClassData(typeof(DataAD06))]
    public async Task CheckInfos(Data data)
    {
        Skip.If(timeoutCount >= 3, "Probably geo-blocked"); // AD06 is geo-restricted, so if the API times out 3 times, we assume it's because the location is blocked.
        Registry registry;
        int pageNumber;
        try { (registry, pageNumber) = await instance.Infos(new Uri(data.URL)); }
        catch (Exception? e)
        {
            var innerEx = e;
            while (innerEx is not TimeoutException or TaskCanceledException or null) innerEx = innerEx?.InnerException;
            Skip.If(innerEx is TimeoutException or TaskCanceledException, $"Timed-out ({++timeoutCount})");
            throw;
        }

        output.WriteLine(await Json.StringifyAsync(registry));

        Assert.Equal(instance.GetType().Name, registry.ProviderId);
        Assert.Equal(data.Id, registry.Id);
        Assert.Equal(data.Page, pageNumber);
        Assert.Equal(data.Cote, registry.CallNumber);
        Assert.Equal(data.Titre, registry.Title);
        Assert.Equal(data.SousTitre, registry.Subtitle);
        Assert.Equal(data.Auteur, registry.Author);
        Assert.Equal(data.From, registry.From);
        Assert.Equal(data.To, registry.To);

        var pos = new List<string>(data.Details ?? []);
        if (data.Ville != null) pos.Add(data.Ville);
        if (data.Paroisse != null) pos.Add(data.Paroisse);
        if (pos.Contains("2e bureau de Nice 1914-1955 (autres communes)"))
        {
            output.WriteLine(string.Join(", ", pos));
        }
        Assert.Equal(pos, registry.Location);

        var types = registry.Types.ToArray();
        Assert.All(data.Types, type => Assert.Contains(type, types));
        Assert.Equal(data.Types.Length, types.Length);
    }
}
