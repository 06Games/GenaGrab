using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace GeneaGrab.Core.Helpers;

public class IiifManifest : IiifManifest<IiifSequence<IiifCanvas<IiifImage>>>
{
    public IiifManifest(string manifest) : base(manifest) { }
}
public class IiifManifest<TSequence> : IIiifManifest where TSequence : IIiifSequence
{
    protected IiifManifest(string manifest) : this(JObject.Parse(manifest)) { }
    internal IiifManifest(JToken manifest)
    {
        MetaData = new ReadOnlyDictionary<string, string>(manifest["metadata"]?.ToDictionary(m => m.Value<string>("label"), m => m.Value<string>("value")) ?? new Dictionary<string, string>());
        Sequences = manifest["sequences"]?.Select(s => (IIiifSequence)Activator.CreateInstance(typeof(TSequence), s)).ToArray() ?? Array.Empty<IIiifSequence>();
    }

    public ReadOnlyDictionary<string, string> MetaData { get; }
    public IIiifSequence[] Sequences { get; }
}

// ReSharper disable ClassNeverInstantiated.Global
public class IiifSequence<TCanvas> : IIiifSequence where TCanvas : IIiifCanvas
{
    public IiifSequence(JToken sequence)
    {
        Id = sequence.Value<string>("@id");
        Label = sequence.Value<string>("@label") ?? sequence.Value<string>("label");
        Canvases = sequence["canvases"]?.Select(s => (IIiifCanvas)Activator.CreateInstance(typeof(TCanvas), s)).ToArray() ?? Array.Empty<IIiifCanvas>();
    }

    public string Id { get; }
    public string Label { get; }
    public IIiifCanvas[] Canvases { get; }
}
public class IiifCanvas<TImage> : IIiifCanvas where TImage : IIiifImage
{
    [SuppressMessage("ReSharper", "MemberCanBeProtected.Global")]
    public IiifCanvas(JToken canvas)
    {
        Id = canvas.Value<string>("@id");
        Label = canvas.Value<string>("label") ?? canvas.Value<string>("@label");
        Thumbnail = canvas["thumbnail"]?.HasValues ?? false ? canvas["thumbnail"].Value<string>("@id") : canvas.Value<string>("thumbnail");
        Images = canvas["images"]?.Select(s => (IIiifImage)Activator.CreateInstance(typeof(TImage), s)).ToArray() ?? Array.Empty<IIiifImage>();
    }

    public string Id { get; }
    public string Label { get; }
    public string Thumbnail { get; }
    public IIiifImage[] Images { get; }
}
public class IiifImage : IIiifImage
{
    public IiifImage(JToken image)
    {
        Id = image.Value<string>("@id");
        var resource = image["resource"];
        Format = resource?.Value<string>("format");
        Width = resource?.Value<int?>("width");
        Height = resource?.Value<int?>("height");
        ServiceId = resource?["service"]?.Value<string>("@id");
    }

    public string Id { get; }
    public string Format { get; }
    public int? Width { get; }
    public int? Height { get; }
    public string ServiceId { get; }
}
