using System.Reflection;
using System.Runtime.InteropServices;
using LoipvRemote.Infrastructure.Windows.Interop;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Hosting;

public sealed class NativeFocusInteropTests
{
    [Test]
    public void FocusAndWindowPlacementStructuresMatchTheWindowsAbi()
    {
        var focusInfo = typeof(NativeMethods).GetNestedType("GUITHREADINFO", BindingFlags.NonPublic)!;

        Assert.Multiple(() =>
        {
            Assert.That(Marshal.SizeOf<NativeMethods.POINT>(), Is.EqualTo(8));
            Assert.That(Marshal.SizeOf<NativeMethods.RECT>(), Is.EqualTo(16));
            Assert.That(Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>(), Is.EqualTo(44));
            Assert.That(Marshal.SizeOf(focusInfo), Is.EqualTo(IntPtr.Size == 8 ? 72 : 48));
        });
    }
}
