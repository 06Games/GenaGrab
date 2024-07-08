using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using GeneaGrab.Core.Models;
using GeneaGrab.Core.Models.Dates;
using GeneaGrab.Helpers;
using GeneaGrab.Models.Indexing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace GeneaGrab.Services;

public class DatabaseContext : DbContext
{
    private sealed class JsonConverter<T>() : ValueConverter<T, string>(v => JsonConvert.SerializeObject(v), v => JsonConvert.DeserializeObject<T>(v)!);
    private sealed class RectConverter() : ValueConverter<Rect, string>(v => v.ToString(), v => Rect.Parse(v));

    public DbSet<Registry> Registries { get; set; } = null!;
    public DbSet<Frame> Frames { get; set; } = null!;
    public DbSet<Record> Records { get; set; } = null!;
    public DbSet<Person> Persons { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!Directory.Exists(LocalData.AppData)) Directory.CreateDirectory(LocalData.AppData);
        optionsBuilder.UseSqlite($"Data Source={LocalData.AppData}/data.db");
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>(); // Store enums as string
        configurationBuilder.Properties<Date>().HaveConversion<JsonConverter<Date>>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<Record>(e =>
            {
                e.Property(b => b.Position).HasConversion<RectConverter>();
                e.HasOne(r => r.Frame).WithMany().HasForeignKey(r => new { r.ProviderId, r.RegistryId, r.FrameNumber });
            })
            .Entity<Registry>(e =>
            {
                e.HasMany(r => r.Frames).WithOne(f => f.Registry).HasForeignKey(r => new { r.ProviderId, r.RegistryId });
                e.Property(r => r.Extra).HasConversion<JsonConverter<object>>();
                e.Property(r => r.Types).HasConversion(
                    v => JsonConvert.SerializeObject(v, Formatting.None, new StringEnumConverter()),
                    v => JsonConvert.DeserializeObject<List<RegistryType>>(v)!);
            })
            .Entity<Frame>(e =>
            {
                e.Property(f => f.Extra).HasConversion<JsonConverter<object>>();
            });
    }
}
