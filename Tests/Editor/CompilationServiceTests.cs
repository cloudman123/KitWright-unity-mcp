// Copyright (C) KitWright. Licensed under MIT.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KitWright.Editor.Services;
using KitWright.Editor.Tools.Builtins;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor.Compilation;

namespace KitWright.Editor.Tests
{
    public sealed class CompilationServiceTests
    {
        [Test]
        public void ResolveIsCompiling_IgnoresRawFlagWhileNoPipelineCompileIsRunning()
        {
            Assert.IsFalse(CompilationService.ResolveIsCompiling(true, false),
                "A deferred reload (LockReloadAssemblies) leaves the raw flag true with nothing compiling.");
            Assert.IsTrue(CompilationService.ResolveIsCompiling(true, true));
            Assert.IsFalse(CompilationService.ResolveIsCompiling(false, true));
        }

        // get_editor_state is what agents poll to decide whether to wait, so it has to report the
        // resolved flag rather than EditorApplication.isCompiling.
        [Test]
        public void GetEditorState_ReportsTheResolvedCompilingFlag()
        {
            var expected = CompilationService.IsActuallyCompiling;
            var response = JObject.FromObject(EditorStateFunctions.GetEditorState());

            Assert.IsTrue(response.Value<bool>("success"), response.ToString());
            Assert.AreEqual(expected, response["data"].Value<bool>("isCompiling"));
            Assert.IsFalse(expected, "Tests only run once compilation finished, so nothing should be compiling.");
        }

        [Test]
        public void GetCompilationErrors_ReportsTotalAndShownCountsWhenTruncated()
        {
            var field = typeof(CompilationService).GetField("LatestMessages",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "LatestMessages was renamed; update this test.");

            var messages = (List<CompilerMessage>)field.GetValue(null);
            var backup = messages.ToList();

            try
            {
                messages.Clear();
                for (var i = 0; i < 3; i++)
                {
                    messages.Add(new CompilerMessage
                    {
                        message = "error " + i,
                        file = "Assets/Fake.cs",
                        line = i + 1,
                        type = CompilerMessageType.Error
                    });
                }

                var truncated = CompilationService.Instance.GetCompilationErrors(maxEntries: 1);
                StringAssert.Contains("3 total", truncated);
                StringAssert.Contains("showing first 1", truncated);
                Assert.That(truncated.Split('\n').Count(line => line.StartsWith("- [")), Is.EqualTo(1));

                var complete = CompilationService.Instance.GetCompilationErrors(maxEntries: 10);
                StringAssert.Contains("3 total", complete);
                Assert.That(complete, Does.Not.Contain("showing first"));
                Assert.That(complete.Split('\n').Count(line => line.StartsWith("- [")), Is.EqualTo(3));
            }
            finally
            {
                messages.Clear();
                messages.AddRange(backup);
            }
        }
    }
}
