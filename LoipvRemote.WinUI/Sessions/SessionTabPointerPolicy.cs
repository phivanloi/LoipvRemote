namespace LoipvRemote.WinUI.Sessions;

/// <summary>Defines when a pointer action should close a session tab.</summary>
internal static class SessionTabPointerPolicy
{
    public static bool ShouldClose(bool middleButtonPressed, bool isClosable) =>
        middleButtonPressed && isClosable;

    public static bool ContainsPoint(
        double pointX,
        double pointY,
        double left,
        double top,
        double width,
        double height) =>
        width > 0 && height > 0 &&
        pointX >= left && pointX < left + width &&
        pointY >= top && pointY < top + height;
}
