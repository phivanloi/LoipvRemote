using System.Runtime.Versioning;
using System.Windows.Forms;

namespace LoipvRemote.UI
{
    [SupportedOSPlatform("windows")]
    public static class TextBoxExtensions
    {
        public static bool SetCueBannerText(this TextBox textBox, string cueText)
        {
            if (!textBox.IsHandleCreated || cueText == null) return false;
            textBox.PlaceholderText = cueText;
            return textBox.PlaceholderText == cueText;
        }

        public static string? GetCueBannerText(this TextBox textBox)
        {
            return string.IsNullOrEmpty(textBox.PlaceholderText) ? null : textBox.PlaceholderText;
        }
    }
}
