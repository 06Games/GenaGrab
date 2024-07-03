using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GeneaGrab.Core.Models;

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
}
