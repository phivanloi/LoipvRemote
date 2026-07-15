using System.Runtime.Versioning;

namespace LoipvRemote.Infrastructure.Windows.ApplicationIdentity;

[SupportedOSPlatform("windows")]
public interface IApplicationIdentityService
{
    void Configure(string appUserModelId);
}
