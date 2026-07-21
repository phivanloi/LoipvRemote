using LoipvRemote.Infrastructure.Windows.WindowEmbedding;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Hosting;

public sealed class EmbeddedWindowFocusControllerTests
{
    [Test]
    public void IsDescendantWindowWalksCrossProcessParentChain()
    {
        var parents = new Dictionary<IntPtr, IntPtr>
        {
            [(IntPtr)30] = (IntPtr)20,
            [(IntPtr)20] = (IntPtr)10,
            [(IntPtr)10] = IntPtr.Zero
        };

        Assert.That(
            WindowSessionHotKeyController.IsDescendantWindow(
                (IntPtr)10,
                (IntPtr)30,
                window => parents.GetValueOrDefault(window)),
            Is.True);
        Assert.That(
            WindowSessionHotKeyController.IsDescendantWindow(
                (IntPtr)99,
                (IntPtr)30,
                window => parents.GetValueOrDefault(window)),
            Is.False);
    }

    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    [TestCase(true, true, true)]
    [TestCase(false, false, false)]
    public void ShouldHandleTabShortcutUsesStableActivationOrForegroundOwnership(
        bool enabled,
        bool ownsForeground,
        bool expected)
    {
        Assert.That(
            WindowSessionHotKeyController.ShouldHandleTabShortcut(enabled, ownsForeground),
            Is.EqualTo(expected));
    }

    [TestCase(true, true, 1, true)]
    [TestCase(true, true, -1, true)]
    [TestCase(false, true, 1, false)]
    [TestCase(true, false, 1, false)]
    [TestCase(true, true, 0, false)]
    public void TabNavigationIsDispatchedOnlyAfterTheCapturedTabKeyIsReleased(
        bool keyUp,
        bool tabChordActive,
        int pendingDirection,
        bool expected)
    {
        Assert.That(
            WindowSessionHotKeyController.ShouldDispatchNavigation(
                keyUp,
                tabChordActive,
                pendingDirection),
            Is.EqualTo(expected));
    }

    [Test]
    public void TabNavigationOnlyQueuesNavigationFromTheLowLevelHookThread()
    {
        var events = new List<string>();

        WindowSessionHotKeyController.DispatchNavigation(
            1,
            direction => events.Add($"navigate:{direction}"));

        Assert.That(events, Is.EqualTo(["navigate:1"]));
    }

    [TestCase(true, true, true, true)]
    [TestCase(true, false, true, false)]
    [TestCase(false, true, true, false)]
    [TestCase(true, true, false, false)]
    public void FirstOrdinaryKeyAfterTabNavigationForcesFocusRecoveryBeforeDispatch(
        bool recoveryPending,
        bool keyDown,
        bool ownsForeground,
        bool expected)
    {
        Assert.That(
            WindowSessionHotKeyController.ShouldRecoverFocusBeforeKeyDispatch(
                recoveryPending,
                keyDown,
                ownsForeground),
            Is.EqualTo(expected));
    }

    [TestCase(true, true)]
    [TestCase(false, false)]
    public void MiddleClickIsSuppressedOnlyWhenTheApplicationHandledATab(bool handled, bool expected)
    {
        Assert.That(WindowSessionPointerController.ShouldSuppressMiddleClick(handled), Is.EqualTo(expected));
    }

    [TestCase(10, 10, false, true)]
    [TestCase(10, 20, true, true)]
    [TestCase(10, 20, false, false)]
    public void SessionHotKeysRemainActiveForTheOwnerOrEmbeddedChild(
        int ownerValue,
        int foregroundValue,
        bool isChild,
        bool expected)
    {
        bool result = WindowSessionHotKeyController.OwnsForegroundWindow(
            (IntPtr)ownerValue,
            (IntPtr)foregroundValue,
            (_, _) => isChild);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void TryFocusBringsOwnerForwardAndVerifiesEmbeddedFocus()
    {
        var owner = (IntPtr)10;
        var embedded = (IntPtr)20;
        IntPtr foreground = IntPtr.Zero;
        IntPtr focused = IntPtr.Zero;
        var attachments = new List<bool>();
        var controller = new EmbeddedWindowFocusController(
            (IntPtr handle, out uint processId) =>
            {
                processId = handle == owner ? 1u : 2u;
                return handle == owner ? 100u : 200u;
            },
            (_, _, attach) =>
            {
                attachments.Add(attach);
                return true;
            },
            handle =>
            {
                IntPtr previous = focused;
                focused = handle;
                return previous;
            },
            () => focused,
            () => foreground,
            handle =>
            {
                foreground = handle;
                return true;
            });

        bool result = controller.TryFocus(owner, embedded);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(foreground, Is.EqualTo(owner));
            Assert.That(focused, Is.EqualTo(embedded));
            Assert.That(attachments, Is.EqualTo([true, false]));
        });
    }

    [Test]
    public void TryFocusDoesNotReactivateWindowsWhenEmbeddedWindowAlreadyHasFocus()
    {
        var owner = (IntPtr)10;
        var embedded = (IntPtr)20;
        IntPtr focused = embedded;
        int foregroundAttempts = 0;
        int focusAttempts = 0;
        var attachments = new List<bool>();
        var controller = new EmbeddedWindowFocusController(
            (IntPtr handle, out uint processId) =>
            {
                processId = handle == owner ? 1u : 2u;
                return handle == owner ? 100u : 200u;
            },
            (_, _, attach) =>
            {
                attachments.Add(attach);
                return true;
            },
            handle =>
            {
                focusAttempts++;
                IntPtr previous = focused;
                focused = handle;
                return previous;
            },
            () => focused,
            () => embedded,
            _ =>
            {
                foregroundAttempts++;
                return true;
            });

        bool result = controller.TryFocus(owner, embedded);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(foregroundAttempts, Is.Zero);
            Assert.That(focusAttempts, Is.Zero);
            Assert.That(attachments, Is.EqualTo([true, false]));
        });
    }

    [Test]
    public void TryFocusReturnsFalseWhenWindowsDidNotKeepEmbeddedFocus()
    {
        var owner = (IntPtr)10;
        var embedded = (IntPtr)20;
        var attachments = new List<bool>();
        var controller = new EmbeddedWindowFocusController(
            (IntPtr handle, out uint processId) =>
            {
                processId = 1;
                return handle == owner ? 100u : 200u;
            },
            (_, _, attach) =>
            {
                attachments.Add(attach);
                return true;
            },
            _ => IntPtr.Zero,
            () => owner,
            () => owner,
            _ => true);

        bool result = controller.TryFocus(owner, embedded);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(attachments, Is.EqualTo([true, false]));
        });
    }

    [Test]
    public void TryFocusStopsWhenOwnerCannotBecomeForeground()
    {
        var owner = (IntPtr)10;
        var embedded = (IntPtr)20;
        bool focusAttempted = false;
        var controller = new EmbeddedWindowFocusController(
            (IntPtr handle, out uint processId) =>
            {
                processId = 1;
                return handle == owner ? 100u : 200u;
            },
            (_, _, _) => true,
            _ =>
            {
                focusAttempted = true;
                return IntPtr.Zero;
            },
            () => IntPtr.Zero,
            () => IntPtr.Zero,
            _ => false);

        bool result = controller.TryFocus(owner, embedded);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(focusAttempted, Is.False);
        });
    }
}
