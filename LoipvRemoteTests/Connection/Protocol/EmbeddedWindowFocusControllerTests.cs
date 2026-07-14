using LoipvRemote.Infrastructure.Windows.WindowEmbedding;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace LoipvRemoteTests.Connection.Protocol;

[TestFixture]
public class EmbeddedWindowFocusControllerTests
{
    [Test]
    public void DetachesCrossThreadInputImmediatelyAfterFocus()
    {
        List<(uint OwnerThread, uint EmbeddedThread, bool Attach)> attachmentCalls = new();
        IntPtr focusedWindow = IntPtr.Zero;
        EmbeddedWindowFocusController controller = CreateController(
            ownerThreadId: 10,
            embeddedThreadId: 20,
            (ownerThread, embeddedThread, attach) =>
            {
                attachmentCalls.Add((ownerThread, embeddedThread, attach));
                return true;
            },
            window => focusedWindow = window);

        bool wasFocused = controller.TryFocus((IntPtr)1, (IntPtr)2);

        Assert.Multiple(() =>
        {
            Assert.That(wasFocused, Is.True);
            Assert.That(focusedWindow, Is.EqualTo((IntPtr)2));
            Assert.That(attachmentCalls, Is.EqualTo(new[] { (10u, 20u, true), (10u, 20u, false) }));
        });
    }

    [Test]
    public void DoesNotFocusWhenCrossThreadAttachmentFails()
    {
        bool wasFocused = false;
        EmbeddedWindowFocusController controller = CreateController(
            ownerThreadId: 10,
            embeddedThreadId: 20,
            (_, _, _) => false,
            _ => wasFocused = true);

        Assert.That(controller.TryFocus((IntPtr)1, (IntPtr)2), Is.False);
        Assert.That(wasFocused, Is.False);
    }

    [Test]
    public void DetachesCrossThreadInputWhenSettingFocusThrows()
    {
        List<bool> attachmentCalls = new();
        EmbeddedWindowFocusController controller = CreateController(
            ownerThreadId: 10,
            embeddedThreadId: 20,
            (_, _, attach) =>
            {
                attachmentCalls.Add(attach);
                return true;
            },
            _ => throw new InvalidOperationException("focus failed"));

        Assert.That(
            () => controller.TryFocus((IntPtr)1, (IntPtr)2),
            Throws.TypeOf<InvalidOperationException>());
        Assert.That(attachmentCalls, Is.EqualTo(new[] { true, false }));
    }

    [Test]
    public void SwitchingEmbeddedWindowsNeverLeavesAnInputQueueAttached()
    {
        List<(uint OwnerThread, uint EmbeddedThread, bool Attach)> attachmentCalls = new();
        EmbeddedWindowFocusController controller = CreateController(
            ownerThreadId: 10,
            embeddedThreadId: 20,
            (ownerThread, embeddedThread, attach) =>
            {
                attachmentCalls.Add((ownerThread, embeddedThread, attach));
                return true;
            },
            _ => { });

        _ = controller.TryFocus((IntPtr)1, (IntPtr)2);
        _ = controller.TryFocus((IntPtr)1, (IntPtr)3);

        Assert.That(attachmentCalls, Is.EqualTo(new[]
        {
            (10u, 20u, true),
            (10u, 20u, false),
            (10u, 20u, true),
            (10u, 20u, false)
        }));
    }

    [Test]
    public void FocusesSameThreadWindowWithoutAttachingQueues()
    {
        bool attachCalled = false;
        bool wasFocused = false;
        EmbeddedWindowFocusController controller = CreateController(
            ownerThreadId: 10,
            embeddedThreadId: 10,
            (_, _, _) =>
            {
                attachCalled = true;
                return true;
            },
            _ => wasFocused = true);

        Assert.That(controller.TryFocus((IntPtr)1, (IntPtr)2), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(attachCalled, Is.False);
            Assert.That(wasFocused, Is.True);
        });
    }

    [Test]
    public void RejectsMissingWindowHandles()
    {
        EmbeddedWindowFocusController controller = CreateController(10, 20, (_, _, _) => true, _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(controller.TryFocus(IntPtr.Zero, (IntPtr)2), Is.False);
            Assert.That(controller.TryFocus((IntPtr)1, IntPtr.Zero), Is.False);
        });
    }

    private static EmbeddedWindowFocusController CreateController(
        uint ownerThreadId,
        uint embeddedThreadId,
        Func<uint, uint, bool, bool> attachThreadInput,
        Action<IntPtr> setFocus)
    {
        return new EmbeddedWindowFocusController(
            (IntPtr handle, out uint processId) =>
            {
                processId = 1;
                return handle == (IntPtr)1 ? ownerThreadId : embeddedThreadId;
            },
            attachThreadInput,
            window =>
            {
                setFocus(window);
                return IntPtr.Zero;
            });
    }
}
