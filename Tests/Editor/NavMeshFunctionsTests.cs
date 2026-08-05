// Copyright (C) GameWright. Licensed under MIT.

using GameWright.Editor.Tools.Helpers;
using NUnit.Framework;
using UnityEngine;

namespace GameWright.Editor.Tests
{
    public sealed class NavMeshFunctionsTests
    {
        [Test]
        public void TryParseVector3_Valid()
        {
            Assert.IsTrue(ValueParse.TryParseVector3("1,2,3", out var v, out _));
            Assert.AreEqual(new Vector3(1, 2, 3), v);
        }

        [Test]
        public void TryParseVector3_HandlesParenthesesAndSpaces()
        {
            Assert.IsTrue(ValueParse.TryParseVector3("( 1.5 , -2 , 0 )", out var v, out _));
            Assert.AreEqual(new Vector3(1.5f, -2f, 0f), v);
        }

        [Test]
        public void TryParseVector3_TooFewComponents()
        {
            Assert.IsFalse(ValueParse.TryParseVector3("1,2", out _, out _));
        }

        [Test]
        public void TryParseVector3_NonNumeric()
        {
            Assert.IsFalse(ValueParse.TryParseVector3("a,b,c", out _, out _));
        }

        [Test]
        public void TryParseVector3_NullOrEmpty()
        {
            Assert.IsFalse(ValueParse.TryParseVector3(null, out _, out _));
            Assert.IsFalse(ValueParse.TryParseVector3("", out _, out _));
        }
    }
}
