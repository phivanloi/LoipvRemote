using BrightIdeasSoftware;
using LoipvRemote.Connection;
using System.Runtime.Versioning;

namespace LoipvRemote.UI.Controls.ConnectionTree
{
    [SupportedOSPlatform("windows")]
    public class NameColumn : OLVColumn
    {
        public NameColumn(ImageGetterDelegate imageGetterDelegate)
        {
            AspectName = "Name";
            FillsFreeSpace = false;
            AspectGetter = item => ((ConnectionInfo)item).Name;
            ImageGetter = imageGetterDelegate;
            AutoCompleteEditor = false;
        }
    }
}