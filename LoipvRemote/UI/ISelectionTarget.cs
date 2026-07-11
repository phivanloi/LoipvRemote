using System.Drawing;

namespace LoipvRemote.UI
{
    public interface ISelectionTarget<out T>
    {
        string Text { get; set; }
        Image Image { get; }
        T Config { get; }
    }
}