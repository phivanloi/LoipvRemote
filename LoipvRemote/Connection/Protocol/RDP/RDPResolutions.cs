using LoipvRemote.Tools;
using LoipvRemote.Resources.Language;

namespace LoipvRemote.Connection.Protocol.RDP
{
    public enum RDPResolutions
    {
        [LocalizedAttributes.LocalizedDescription(nameof(Language.SmartSize))]
        SmartSize,

        [LocalizedAttributes.LocalizedDescription(nameof(Language.FitToPanel))]
        FitToWindow,

        [LocalizedAttributes.LocalizedDescription(nameof(Language.Fullscreen))]
        Fullscreen
    }
}