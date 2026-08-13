// Copyright (C) KitWright. Licensed under MIT.

using KitWright.Editor.Tools.Builtins;
using KitWright.Editor.Tools.Scripting;
using NUnit.Framework;

namespace KitWright.Editor.Tests
{
    public sealed class CompiledCodeGuardTests
    {
        // The strict source rule anchors on "File." with a (?<![\w.]) lookbehind, or on the literal
        // "System.IO.File.", so a namespace alias slips past it while still binding to the same method.
        private const string AliasedWrite = @"
using IO = System.IO;

public class Aliased
{
    public static string Run()
    {
        IO.File.WriteAllText(""probe.txt"", ""hello"");
        return ""done"";
    }
}";

        private const string PlainWrite = @"
using System.IO;

public class Writer
{
    public static string Run()
    {
        File.WriteAllText(""probe.txt"", ""hello"");
        return ""done"";
    }
}";

        private const string ReflectedDelete = @"
public class Reflected
{
    public static string Run()
    {
        var handle = typeof(System.IO.File);
        handle.GetMethod(""Delete"", new[] { typeof(string) });
        return ""done"";
    }
}";

        private const string Benign = @"
using UnityEngine;

public class Benign
{
    public static string Run()
    {
        return Application.unityVersion;
    }
}";

        [Test]
        public void SourcePolicy_MissesAnAliasedNamespace()
        {
            Assert.IsFalse(ExecuteCodeSafetyPolicy.TryFindViolation(AliasedWrite, true, out _, out _));
        }

        [Test]
        public void Guard_CatchesTheCallTheSourcePolicyMissed()
        {
            var compilation = ScriptCompilerPipeline.Compile(AliasedWrite);
            Assert.AreEqual(ScriptCompilationStatus.Success, compilation.Status, compilation.Message);

            Assert.IsTrue(CompiledCodeGuard.TryFindViolation(compilation.Assembly, true, out var reference, out _));
            Assert.AreEqual("System.IO.File.WriteAllText", reference);
        }

        // Reflection that resolves the member by name is out of reach: the assembly binds to
        // Type.GetMethod, not to the method it ends up calling. The source rules catch this one.
        [Test]
        public void Guard_DoesNotSeeThroughReflectionDispatch()
        {
            var compilation = ScriptCompilerPipeline.Compile(ReflectedDelete);
            Assert.AreEqual(ScriptCompilationStatus.Success, compilation.Status, compilation.Message);

            Assert.IsFalse(CompiledCodeGuard.TryFindViolation(compilation.Assembly, true, out _, out _));
            Assert.IsTrue(ExecuteCodeSafetyPolicy.TryFindViolation(ReflectedDelete, true, out _, out _));
        }

        [Test]
        public void Guard_BlocksAStrictFilesystemWriteOnlyWhenStrict()
        {
            var compilation = ScriptCompilerPipeline.Compile(PlainWrite);
            Assert.AreEqual(ScriptCompilationStatus.Success, compilation.Status, compilation.Message);

            Assert.IsTrue(CompiledCodeGuard.TryFindViolation(compilation.Assembly, true, out var reference, out _));
            Assert.AreEqual("System.IO.File.WriteAllText", reference);

            Assert.IsFalse(CompiledCodeGuard.TryFindViolation(compilation.Assembly, false, out _, out _));
        }

        [Test]
        public void Guard_LetsAnOrdinarySnippetThrough()
        {
            var compilation = ScriptCompilerPipeline.Compile(Benign);
            Assert.AreEqual(ScriptCompilationStatus.Success, compilation.Status, compilation.Message);

            Assert.IsFalse(CompiledCodeGuard.TryFindViolation(compilation.Assembly, true, out _, out _));
        }
    }
}
