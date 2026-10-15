using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.WinUI.Hosting;

internal static class OverlayOcclusionPolicy
{
    public static EmbeddedWindowBounds? ToHostLocalHole(
        EmbeddedWindowBounds hostScreenBounds,
        EmbeddedWindowBounds overlayScreenBounds,
        int padding)
    {
        if (!hostScreenBounds.IsValid || !overlayScreenBounds.IsValid)
            return null;

        long safePadding = Math.Max(0, padding);
        long overlayLeft = (long)overlayScreenBounds.X - safePadding;
        long overlayTop = (long)overlayScreenBounds.Y - safePadding;
        long overlayRight = (long)overlayScreenBounds.X + overlayScreenBounds.Width + safePadding;
        long overlayBottom = (long)overlayScreenBounds.Y + overlayScreenBounds.Height + safePadding;
        long hostRight = (long)hostScreenBounds.X + hostScreenBounds.Width;
        long hostBottom = (long)hostScreenBounds.Y + hostScreenBounds.Height;

        long left = Math.Max(hostScreenBounds.X, overlayLeft);
        long top = Math.Max(hostScreenBounds.Y, overlayTop);
        long right = Math.Min(hostRight, overlayRight);
        long bottom = Math.Min(hostBottom, overlayBottom);
        if (right <= left || bottom <= top)
            return null;

        return new EmbeddedWindowBounds(
            checked((int)(left - hostScreenBounds.X)),
            checked((int)(top - hostScreenBounds.Y)),
            checked((int)(right - left)),
            checked((int)(bottom - top)));
    }
}
