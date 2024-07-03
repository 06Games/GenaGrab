using Newtonsoft.Json.Linq;

namespace GeneaGrab.Core.Helpers;

public class LigeoManifest : IiifManifest<IiifSequence<LigeoCanvas>>
{
    public LigeoManifest(string manifest) : this(JObject.Parse(manifest)) { }
    private LigeoManifest(JToken manifest) : base(manifest)
    {
        IsIndexable = manifest.Value<bool>("ligeoIsIndexable");
    }

    public bool IsIndexable { get; }
}

// ReSharper disable ClassNeverInstantiated.Global
public class LigeoCanvas : IiifCanvas<IiifImage>
{
    public LigeoCanvas(JToken canvas) : base(canvas)
    {
        Permalink = canvas.Value<string>("ligeoPermalink");
        RestrictedAccess = canvas.Value<bool>("ligeoRestrictedAccess");
        RestrictMessage = canvas.Value<string>("ligeoRestrictMessage");
        IsSearchable = canvas.Value<bool>("ligeoIsSearchable");
        var classeur = canvas["ligeoClasseur"];
        Classeur = classeur == null ? null : new LigeoClasseur(classeur);
        MediaPath = canvas.Value<string>("ligeoMediaPath");
    }

    public string Permalink { get; }
    public bool RestrictedAccess { get; }
    public string RestrictMessage { get; }
    public bool IsSearchable { get; }
    public LigeoClasseur Classeur { get; }
    public string MediaPath { get; }
}
public class LigeoClasseur
{
    public LigeoClasseur(JToken classeur)
    {
        ImageBase = classeur.Value<string>("strImageBase");
        ImageDir = classeur.Value<string>("strImageDir");
        Tag = classeur.Value<string>("curTag");
        TagNumber = classeur.Value<int>("curTagnum");
        NoticeId = classeur.Value<string>("notice_id");
        Ark = classeur.Value<string>("ark");
        UnitId = classeur.Value<string>("unitid");
        EncodedArchivalDescriptionId = classeur.Value<string>("eadid");
    }
    public string ImageDir { get; }
    public string ImageBase { get; }
    public string Tag { get; }
    public int TagNumber { get; }
    public string NoticeId { get; }
    public string Ark { get; }
    public string UnitId { get; }
    public string EncodedArchivalDescriptionId { get; }
}
