namespace GeneaGrab.Core.Models;

public sealed class RegistryInfo
{
    public RegistryInfo(Provider provider, string registryId) : this(provider.Id, registryId) { }
    public RegistryInfo(Registry r) : this(r.ProviderId, r.Id) { }
    private RegistryInfo(string providerId, string registryId)
    {
        ProviderId = providerId;
        RegistryId = registryId;
    }

    public string ProviderId { get; }
    public string RegistryId { get; }
    public int? PageNumber { get; init; }
    public string FrameArkUrl { get; init; }
}
