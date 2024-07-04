using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GeneaGrab.Core.Models;
using Serilog;

namespace GeneaGrab.Core.Helpers;

public abstract class Ligeo : Iiif
{
    protected Ligeo(HttpClient client) : base(client) { }


    protected abstract string Host { get; }
    public override string Url => $"https://{Host}/";


    public override Task<RegistryInfo> GetRegistryFromUrlAsync(Uri url)
    {
        if (url.Host != Host || !url.AbsolutePath.StartsWith("/ark:/")) return Task.FromResult<RegistryInfo>(null);

        var queries = Regex.Match(url.AbsolutePath, @"/ark:/(?<something>[\w\.]+)(/(?<id>[\w\.]+))?(/(?<tag>[\w\.]+))?(/(?<seq>\d+))?(/(?<page>\d+))?").Groups;
        return Task.FromResult(new RegistryInfo(this, queries["id"].Value) { PageNumber = int.TryParse(queries["page"].Value, out var page) ? page : 1 });
    }

    public override Task<string> Ark(Frame page) => Task.FromResult(page.Registry == null ? null : $"{page.Registry.ArkURL}/{page.FrameNumber}");


    protected override Task<(Registry registry, int sequence, object page)> ParseUrl(Uri url)
    {
        var queries = Regex.Match(url.AbsolutePath, @"/ark:/(?<something>[\w\.]+)(/(?<id>[\w\.]+))?(/(?<tag>[\w\.]+))?(/(?<seq>\d+))?(/(?<page>\d+))?").Groups;
        var registry = new Registry(Id, queries["id"].Value);
        registry.URL = $"{Url}ark:/{queries["something"].Value}/{registry.Id}";
        if (!int.TryParse(queries["seq"].Value, out var seq)) Log.Warning("Couldn't parse sequence ({SequenceValue}), using default one", queries["seq"].Value);
        return Task.FromResult<(Registry, int, object)>((registry, seq, int.TryParse(queries["page"].Value, out var page) ? page : 1));
    }

    protected override IIiifManifest ParseManifest(string stringAsync) => new LigeoManifest(stringAsync);

    protected override Frame CreateFrame(IIiifCanvas p, int i)
    {
        var canvas = p as LigeoCanvas;
        var frame = base.CreateFrame(p, i);
        frame.Extra = canvas?.Classeur;
        return frame;
    }

    protected virtual Task ParseClasseur(Registry registry, IIiifSequence sequence, LigeoClasseur classeur)
    {
        if (classeur != null)
            registry.CallNumber = string.IsNullOrWhiteSpace(classeur.UnitId) ? null : classeur.UnitId;
        return Task.CompletedTask;
    }

    protected override async Task ReadSequence(Registry registry, IIiifSequence sequence)
    {
        await base.ReadSequence(registry, sequence);
        var canvas = sequence.Canvases[0] as LigeoCanvas;
        var classeur = canvas?.Classeur;
        await ParseClasseur(registry, sequence, classeur);

        registry.ArkURL = sequence.Id;
    }

    protected override int GetPage(Registry registry, object page) => (int)page;
}
