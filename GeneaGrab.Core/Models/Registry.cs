#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using GeneaGrab.Core.Models.Dates;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace GeneaGrab.Core.Models;

/// <summary>Data on the registry</summary>
[PrimaryKey(nameof(ProviderId), nameof(Id))]
public sealed class Registry : IEquatable<Registry>
{
    public Registry(Provider provider, string id) : this(provider.Id, id) { }
    public Registry(string providerId, string id)
    {
        ProviderId = providerId;
        Id = id;
    }

    public string ProviderId { get; init; }
    /// <summary>Associated provider</summary>
    [NotMapped, JsonIgnore] public Provider Provider => Data.Providers[ProviderId];

    public string Id { get; init; }

    /// <summary>Call number of the document (if applicable)</summary>
    public string? CallNumber { get; set; }
    public string? URL { get; set; }
    public string? ArkURL { get; set; }
    public IList<RegistryType> Types { get; set; } = new List<RegistryType>();

    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? Author { get; set; }
    public IList<string> Location { get; set; } = new List<string>();
    public string? Notes { get; set; }


    /// <summary>Any additional information that might be needed</summary>
    public object? Extra { get; set; }

    public Date? From { get; set; }
    public Date? To { get; set; }


    public override string ToString() => $"{ProviderId}@{Id}";

    public IList<Frame> Frames { get; set; } = new List<Frame>();


    public bool Equals(Registry? other) => Id == other?.Id;
    public override bool Equals(object? obj) => Equals(obj as Registry);
    public static bool operator ==(Registry? one, Registry? two) => one?.Id == two?.Id;
    public static bool operator !=(Registry? one, Registry? two) => !(one == two);
    public override int GetHashCode() => Id.GetHashCode();
}
