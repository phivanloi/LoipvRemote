using System.Runtime.Versioning;

namespace LoipvRemote.Infrastructure.Windows.WindowActivation;

[SupportedOSPlatform("windows")]
public interface IWindowActivationService
{
    bool Activate(nint windowHandle);

    bool RestoreAndActivate(nint windowHandle);
}
