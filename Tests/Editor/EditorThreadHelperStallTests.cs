// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.Threading;
using System.Threading.Tasks;
using KitWright.Editor.Threading;
using NUnit.Framework;
using UnityEngine;

namespace KitWright.Editor.Tests
{
    public sealed class EditorThreadHelperStallTests
    {
        [Test]
        public void LooksBlocked_SeparatesAStalledEditorFromAMerelySlowTool()
        {
            Assert.IsFalse(Blocked(TimeSpan.FromMilliseconds(200)),
                "A slow but pumping editor must not be reported as blocked.");
            Assert.IsFalse(Blocked(TimeSpan.FromSeconds(4)),
                "Under the staleness threshold is still healthy.");

            Assert.IsTrue(Blocked(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(Blocked(TimeSpan.FromMinutes(2)));

            Assert.IsFalse(
                EditorThreadHelper.LooksBlocked(true, TimeSpan.FromMinutes(2), false, false),
                "A call that already returned must never be failed after the fact.");
        }

        // CoplayDev/unity-mcp #1130, #1341.
        [Test]
        public void LooksBlocked_LeavesOurOwnLongSynchronousToolToItsTimeoutBudget()
        {
            Assert.IsFalse(
                EditorThreadHelper.LooksBlocked(false, TimeSpan.FromMinutes(6), true, false),
                "A six-minute build is not a blocked editor; its [LongRunningTool] ceiling owns it.");

            Assert.IsTrue(
                EditorThreadHelper.LooksBlocked(false, TimeSpan.FromMinutes(6), true, true),
                "A modal our own work item opened is still a block - that is what the probe is for.");

            Assert.IsTrue(
                EditorThreadHelper.LooksBlocked(false, TimeSpan.FromMinutes(6), false, false),
                "Nothing of ours is running, so a stale pump is someone else blocking the editor.");
        }

        [Test]
        public void WorkItemRunning_IsTrueInsideTheWorkItemAndFalseOutsideIt()
        {
            Assert.IsFalse(EditorThreadHelper.WorkItemRunning, "Nothing is mid-flight before the pump.");

            PumpOnce(cancelBeforePump: false, out var sawWorkItemRunning);

            Assert.IsTrue(sawWorkItemRunning,
                "The flag has to be set around the invoke, or the probe cannot see a build.");
            Assert.IsFalse(EditorThreadHelper.WorkItemRunning, "The finally must clear it again.");
        }

        private static bool Blocked(TimeSpan sinceLastPump) =>
            EditorThreadHelper.LooksBlocked(false, sinceLastPump, false, false);

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
        public void BlockedMessage_NamesTheDialogWhenTheProbeIdentifiedOne()
        {
            var message = EditorThreadHelper.BlockedMessage(
                TimeSpan.FromSeconds(21), "Scene(s) Have Been Modified [buttons: Save | Don't Save | Cancel]");

            StringAssert.Contains("EDITOR_NOT_PUMPING", message);
            StringAssert.Contains("Scene(s) Have Been Modified [buttons: Save | Don't Save | Cancel]", message);
            Assert.IsFalse(message.Contains("The usual cause"),
                "A named dialog must replace the guess, not sit next to it.");
        }

        [Test]
        public void BlockingDialog_ReportsNothingWhileTheEditorIsUnblocked()
        {
            Assert.IsNull(Win32Dialogs.BlockingDialog(),
                "No modal is open while this test runs, so the probe must not name one.");
        }

        [Test]
        public void SinceLastPump_IsFreshWhileTheEditorIsRunningThisTest()
        {
            // Batchmode drives the loop only between tests, so the live pump clock is meaningless
            // here — CI measured 23s while the run was perfectly healthy.
            if (Application.isBatchMode)
                Assert.Ignore("The editor loop does not tick during a batchmode test body.");

            Assert.Less(EditorThreadHelper.SinceLastPump.TotalSeconds, 5,
                "The editor is pumping while this test runs, so the watchdog must see it as healthy.");
        }

        [Test]
        public void QueuedWork_RunsOnTheNextPump()
        {
            Assert.IsTrue(PumpOnce(cancelBeforePump: false),
                "A live work item must still run, or the abandon check is passing vacuously.");
        }

        [Test]
        public void QueuedWork_IsDroppedWhenItsCallerAlreadyGaveUp()
        {
            Assert.IsFalse(PumpOnce(cancelBeforePump: true),
                "A call whose deadline passed must never mutate the project after the fact.");
        }

        private static bool PumpOnce(bool cancelBeforePump) =>
            PumpOnce(cancelBeforePump, out _);

        private static bool PumpOnce(bool cancelBeforePump, out bool sawWorkItemRunning)
        {
            var ran = false;
            var sawFlag = false;

            using (var helper = new EditorThreadHelper())
            using (var cts = new CancellationTokenSource())
            {
                // Queued from a worker thread so it lands in the queue instead of running inline.
                // Wait for the CALL to return, not the task it returns: that one only completes once
                // ProcessQueues runs the item, and this thread is the one that has to pump it.
                using (var handoff = new ManualResetEventSlim())
                {
                    Task.Run(() =>
                    {
                        helper.ExecuteAsyncOnEditorThreadAsync(() =>
                        {
                            ran = true;
                            sawFlag = EditorThreadHelper.WorkItemRunning;
                            return Task.FromResult(true);
                        }, cts.Token);
                        handoff.Set();
                    });

                    Assert.IsTrue(handoff.Wait(TimeSpan.FromSeconds(5)), "The work item was never queued.");
                }

                if (cancelBeforePump)
                    cts.Cancel();

                helper.ProcessQueues();
            }

            sawWorkItemRunning = sawFlag;
            return ran;
        }
    }
}
