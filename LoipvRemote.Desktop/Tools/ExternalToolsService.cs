using System.Linq;
using System.Runtime.Versioning;
using LoipvRemote.Tools.CustomCollections;

namespace LoipvRemote.Tools
{
    [SupportedOSPlatform("windows")]
    public class ExternalToolsService
    {
        public FullyObservableCollection<ExternalTool> ExternalTools { get; set; } = [];

        public ExternalTool? GetExtAppByName(string name)
        {
            return ExternalTools.FirstOrDefault(extA => extA.DisplayName == name);
        }
    }
}
