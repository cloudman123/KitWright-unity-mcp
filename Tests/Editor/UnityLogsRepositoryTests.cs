// Copyright (C) KitWright. Licensed under MIT.

using KitWright.Editor.Services.UnityLogs;
using NUnit.Framework;
using UnityEngine;

namespace KitWright.Editor.Tests
{
    public sealed class UnityLogsRepositoryTests
    {
        [Test]
        public void GetRecentLogs_FiltersGroupsAndTruncatesCachedEntries()
        {
            var token = "KitWrightConsoleGrouping_" + System.Guid.NewGuid().ToString("N");
            var longPayload = new string('x', 360);

            using (var repository = new UnityLogsRepository())
            {
                repository.StartListening();
                repository.Clear();

                Debug.Log(token + " duplicate");
                Debug.Log(token + " duplicate");
                Debug.Log(token + " unique");
                Debug.Log(token + " " + longPayload);

                var grouped = repository.GetRecentLogs(
                    logType: "log",
                    count: 10,
                    sinceSeconds: 0,
                    filterText: token,
                    groupDuplicates: true);

                Assert.That(grouped, Does.Contain("Console logs (4 entries, 3 unique, filter: log, source: cache"));
                Assert.That(grouped, Does.Contain("[LOG] " + token + " duplicate (x2)"));
                Assert.That(grouped, Does.Contain("[LOG] " + token + " unique"));
                Assert.That(grouped, Does.Contain("... (+"));
                Assert.That(grouped.Length, Is.LessThan(1400));

                var filtered = repository.GetRecentLogs(
                    logType: "log",
                    count: 10,
                    sinceSeconds: 0,
                    filterText: token + " unique",
                    groupDuplicates: true);

                Assert.That(filtered, Does.Contain("Console logs (1 entries, filter: log, source: cache"));
                Assert.That(filtered, Does.Contain("[LOG] " + token + " unique"));
                Assert.That(filtered, Does.Not.Contain("duplicate"));

                var ungrouped = repository.GetRecentLogs(
                    logType: "log",
                    count: 10,
                    sinceSeconds: 0,
                    filterText: token + " duplicate",
                    groupDuplicates: false);

                Assert.That(ungrouped, Does.Contain("Console logs (2 entries, filter: log, source: cache"));
                Assert.That(ungrouped, Does.Not.Contain("(x2)"));
            }
        }

        [Test]
        public void LogRaisedOffTheMainThread_IsCapturedWithoutTheMainThreadTicking()
        {
            var token = "KitWrightThreadedLog_" + System.Guid.NewGuid().ToString("N");

            using (var repository = new UnityLogsRepository())
            {
                repository.StartListening();
                repository.Clear();

                // This test body owns the main thread for its whole duration, exactly as a modal
                // dialog does. A main-thread-only subscription cannot deliver anything here.
                System.Threading.Tasks.Task.Run(() => Debug.Log(token)).Wait(5000);

                var logs = repository.GetRecentLogs(logType: "log", count: 10, filterText: token);
                Assert.That(logs, Does.Contain(token));
            }
        }

        [Test]
        public void HelperMethods_HandleEmptyTextAndLongLines()
        {
            Assert.IsTrue(UnityLogsRepository.MatchesTextFilter("Hello Console", "console"));
            Assert.IsFalse(UnityLogsRepository.MatchesTextFilter("Hello Console", "missing"));
            Assert.IsTrue(UnityLogsRepository.MatchesTextFilter(null, null));
            Assert.IsFalse(UnityLogsRepository.MatchesTextFilter(null, "missing"));

            var line = new string('a', 305);
            var truncated = UnityLogsRepository.TruncateLine(line);

            Assert.That(truncated, Does.StartWith(new string('a', 300)));
            Assert.That(truncated, Does.EndWith("... (+5 chars)"));
        }

        [Test]
        public void StripRichText_RemovesUnityMarkupButKeepsOtherAngleBrackets()
        {
            Assert.AreEqual(
                "fail: TestResultCollector missing request id",
                UnityLogsRepository.StripRichText(
                    "<color=#ff6b6b>fail:</color> <color=#58D68D><b>TestResultCollector</b></color> missing request id"));

            Assert.AreEqual("done", UnityLogsRepository.StripRichText("<size=20><i>done</i></size>"));

            Assert.AreEqual("List<int> has 3 items", UnityLogsRepository.StripRichText("List<int> has 3 items"));
            Assert.AreEqual("<node id=\"7\" />", UnityLogsRepository.StripRichText("<node id=\"7\" />"));

            Assert.AreEqual("plain", UnityLogsRepository.StripRichText("plain"));
            Assert.IsNull(UnityLogsRepository.StripRichText(null));
        }

        [Test]
        public void GetRecentLogs_StampsEntriesWithoutBreakingDuplicateGrouping()
        {
            var token = "KitWrightConsoleStamp_" + System.Guid.NewGuid().ToString("N");

            using (var repository = new UnityLogsRepository())
            {
                repository.StartListening();
                repository.Clear();

                Debug.Log(token + " <b>duplicate</b>");
                Debug.Log(token + " <b>duplicate</b>");

                var stamped = repository.GetRecentLogs(
                    logType: "log",
                    count: 10,
                    sinceSeconds: 0,
                    filterText: token,
                    groupDuplicates: true,
                    includeStackTrace: false,
                    includeTimestamps: true);

                Assert.That(stamped, Does.Not.Contain("<b>"));
                Assert.That(stamped, Does.Contain("Console logs (2 entries, 1 unique, filter: log, source: cache"));
                Assert.That(stamped, Does.Match(@"\n\d{2}:\d{2}:\d{2} \[LOG\] " + token + @" duplicate \(x2\)"));

                var unstamped = repository.GetRecentLogs(
                    logType: "log",
                    count: 10,
                    sinceSeconds: 0,
                    filterText: token,
                    groupDuplicates: true);

                Assert.That(unstamped, Does.Contain("[LOG] " + token + " duplicate (x2)"));
                Assert.That(unstamped, Does.Not.Match(@"\n\d{2}:\d{2}:\d{2} \["));
            }
        }

        [Test]
        public void FormatStackTrace_NormalizesLineEndingsAndCapsLength()
        {
            var normalized = UnityLogsRepository.FormatStackTrace("First\r\nSecond\rThird\n");

            Assert.AreEqual("\n    First\n    Second\n    Third", normalized);
            Assert.That(normalized, Does.Not.Contain("\r"));

            var longTrace = new string('s', 2105);
            var truncated = UnityLogsRepository.FormatStackTrace(longTrace);
            Assert.That(truncated, Does.StartWith("\n    " + new string('s', 2000)));
            Assert.That(truncated, Does.EndWith("... (+105 chars)"));
        }
    }
}
