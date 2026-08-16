// Copyright (C) KitWright. Licensed under MIT.

using System;
using KitWright.Editor.Threading;
using NUnit.Framework;

namespace KitWright.Editor.Tests
{
    public sealed class EditorThreadHelperStallTests
    {
        [Test]
        public void LooksBlocked_SeparatesAStalledEditorFromAMerelySlowTool()
        {
            Assert.IsFalse(EditorThreadHelper.LooksBlocked(false, TimeSpan.FromMilliseconds(200)),
                "A slow but pumping editor must not be reported as blocked.");
            Assert.IsFalse(EditorThreadHelper.LooksBlocked(false, TimeSpan.FromSeconds(4)),
                "Under the staleness threshold is still healthy.");

            Assert.IsTrue(EditorThreadHelper.LooksBlocked(false, TimeSpan.FromSeconds(5)));
            Assert.IsTrue(EditorThreadHelper.LooksBlocked(false, TimeSpan.FromMinutes(2)));

            Assert.IsFalse(EditorThreadHelper.LooksBlocked(true, TimeSpan.FromMinutes(2)),
                "A call that already returned must never be failed after the fact.");
        }

        [Test]
        public void BlockedMessage_NamesTheCauseAndTheWayOut()
        {
            var message = EditorThreadHelper.BlockedMessage(TimeSpan.FromSeconds(21));

            StringAssert.Contains("EDITOR_NOT_PUMPING", message);
            StringAssert.Contains("21s", message);
            StringAssert.Contains("modal dialog", message);
            StringAssert.Contains("Scene(s) Have Been Modified", message);
        }

        [Test]
        public void SinceLastPump_IsFreshWhileTheEditorIsRunningThisTest()
        {
            Assert.Less(EditorThreadHelper.SinceLastPump.TotalSeconds, 5,
                "The editor is pumping while this test runs, so the watchdog must see it as healthy.");
        }
    }
}
