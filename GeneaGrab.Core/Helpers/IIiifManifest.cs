using System.Collections.ObjectModel;

namespace GeneaGrab.Core.Helpers;

public interface IIiifManifest
{
    ReadOnlyDictionary<string, string> MetaData { get; }
    IIiifSequence[] Sequences { get; }
}
public interface IIiifSequence
{
    public string Id { get; }
    public string Label { get; }
    public IIiifCanvas[] Canvases { get; }
}
public interface IIiifCanvas
{
    public string Id { get; }
    public string Label { get; }
    public string Thumbnail { get; }
    public IIiifImage[] Images { get; }
}
public interface IIiifImage
{
    public string Id { get; }
    public string Format { get; }
    public int? Width { get; }
    public int? Height { get; }
    public string ServiceId { get; }
}
