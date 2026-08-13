// Copyright (C) KitWright. Licensed under MIT.

using System.Linq;
using KitWright.Editor.Tools.Builtins;
using NUnit.Framework;
using UnityEngine;

namespace KitWright.Editor.Tests
{
    public sealed class ReflectionFunctionsTests
    {
        [Test]
        public void Resolve_ShortUnityTypeName()
        {
            var type = ReflectionFunctions.Resolve("Rigidbody", out _, out var ambiguous);

            Assert.AreEqual(typeof(Rigidbody), type);
            Assert.IsFalse(ambiguous);
        }

        [Test]
        public void Resolve_StaticClassOutsideUnityObjectHierarchy()
        {
            Assert.AreEqual(typeof(Mathf), ReflectionFunctions.Resolve("Mathf", out _, out _));
        }

        [Test]
        public void Resolve_AmbiguousShortNameReportsMatches()
        {
            var type = ReflectionFunctions.Resolve("Object", out var matches, out var ambiguous);

            Assert.IsNull(type);
            Assert.IsTrue(ambiguous);
            Assert.Contains("UnityEngine.Object", matches);
            Assert.Contains("System.Object", matches);
        }

        [Test]
        public void Resolve_FullNameBeatsAmbiguity()
        {
            Assert.AreEqual(typeof(Object), ReflectionFunctions.Resolve("UnityEngine.Object", out _, out var ambiguous));
            Assert.IsFalse(ambiguous);
        }

        [Test]
        public void Resolve_TypoReturnsCandidates()
        {
            var type = ReflectionFunctions.Resolve("Rigibody", out var candidates, out var ambiguous);

            Assert.IsNull(type);
            Assert.IsFalse(ambiguous);
            Assert.Contains("Rigidbody", candidates);
        }

        [Test]
        public void Resolve_UnknownNameReturnsNoCandidates()
        {
            var type = ReflectionFunctions.Resolve("Zzz_NoSuchType_9", out var candidates, out _);

            Assert.IsNull(type);
            Assert.IsEmpty(candidates);
        }

        [Test]
        public void Signatures_IncludeParameterNames()
        {
            var signatures = ReflectionFunctions.Signatures(
                typeof(Rigidbody), "AddForce", ReflectionFunctions.DeclaredMembers);

            Assert.IsNotEmpty(signatures);
            Assert.IsTrue(signatures.Any(s => s.Contains("Vector3 force")), string.Join(" | ", signatures));
        }

        [Test]
        public void Signatures_FallBackToInheritedMembers()
        {
            Assert.IsEmpty(ReflectionFunctions.Signatures(
                typeof(Rigidbody), "GetComponent", ReflectionFunctions.DeclaredMembers));

            Assert.IsNotEmpty(ReflectionFunctions.Signatures(
                typeof(Rigidbody), "GetComponent", ReflectionFunctions.InheritedMembers));
        }

        [Test]
        public void ReflectApi_OpenGenericDoesNotReflectMembers()
        {
            Assert.IsNotNull(ReflectionFunctions.ReflectApi("System.Collections.Generic.List`1"));
        }
    }
}
