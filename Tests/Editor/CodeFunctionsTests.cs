// Copyright (C) GameWright. Licensed under MIT.

using GameWright.Editor.Tools.Builtins;
using NUnit.Framework;

namespace GameWright.Editor.Tests
{
    public sealed class CodeFunctionsTests
    {
        [Test]
        public void ComputeSha256_DeterministicAndLowercase()
        {
            var a = CodeFunctions.ComputeSha256("hello");
            var b = CodeFunctions.ComputeSha256("hello");
            Assert.AreEqual(a, b);
            Assert.AreEqual(64, a.Length);
            Assert.AreEqual(a.ToLowerInvariant(), a);
        }

        [Test]
        public void ComputeSha256_DifferentContentDifferentHash()
        {
            Assert.AreNotEqual(CodeFunctions.ComputeSha256("a"), CodeFunctions.ComputeSha256("b"));
        }

        [Test]
        public void ComputeSha256_NullTreatedAsEmpty()
        {
            Assert.AreEqual(CodeFunctions.ComputeSha256(""), CodeFunctions.ComputeSha256(null));
        }

        [Test]
        public void BraceImbalance_BalancedCode()
        {
            Assert.IsFalse(CodeFunctions.TryGetBraceImbalance("class A { void B() { } }", out _));
        }

        [Test]
        public void BraceImbalance_MissingClose()
        {
            Assert.IsTrue(CodeFunctions.TryGetBraceImbalance("class A { void B() { }", out var line));
            Assert.Greater(line, 0);
        }

        [Test]
        public void BraceImbalance_ExtraClose_ReportsLine()
        {
            Assert.IsTrue(CodeFunctions.TryGetBraceImbalance("class A { }\n}", out var line));
            Assert.AreEqual(2, line);
        }

        [Test]
        public void BraceImbalance_IgnoresBracesInStrings()
        {
            Assert.IsFalse(CodeFunctions.TryGetBraceImbalance("class A { string s = \"}}}{{{\"; }", out _));
        }

        [Test]
        public void BraceImbalance_IgnoresBracesInVerbatimStrings()
        {
            Assert.IsFalse(CodeFunctions.TryGetBraceImbalance("class A { string s = @\"} \"\" }\"; }", out _));
        }

        [Test]
        public void BraceImbalance_IgnoresBracesInComments()
        {
            Assert.IsFalse(CodeFunctions.TryGetBraceImbalance("class A { // }}}\n/* {{{ */ }", out _));
        }

        [Test]
        public void BraceImbalance_IgnoresBracesInCharLiterals()
        {
            Assert.IsFalse(CodeFunctions.TryGetBraceImbalance("class A { char c = '}'; }", out _));
        }

        [Test]
        public void BraceImbalance_EmptyOrNull()
        {
            Assert.IsFalse(CodeFunctions.TryGetBraceImbalance("", out _));
            Assert.IsFalse(CodeFunctions.TryGetBraceImbalance(null, out _));
        }
    }
}
