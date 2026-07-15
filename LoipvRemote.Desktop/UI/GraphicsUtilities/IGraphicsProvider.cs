using System.Drawing;

namespace LoipvRemote.UI.GraphicsUtilities
{
    public interface IGraphicsProvider
    {
        SizeF GetResolutionScalingFactor();
    }
}