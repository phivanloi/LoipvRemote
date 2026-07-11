using LoipvRemote.Tools;
using LoipvRemote.Resources.Language;

namespace LoipvRemote.Connection.Protocol.RDP
{
    public enum RDPSoundQuality
    {
        [LocalizedAttributes.LocalizedDescription(nameof(Language.Dynamic))]
        Dynamic = 0,

        [LocalizedAttributes.LocalizedDescription(nameof(Language.Medium))]
        Medium = 1,

        [LocalizedAttributes.LocalizedDescription(nameof(Language.High))]
        High = 2
    }
}