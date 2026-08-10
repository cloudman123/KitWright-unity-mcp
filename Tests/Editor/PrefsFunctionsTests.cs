// Copyright (C) KitWright. Licensed under MIT.

using KitWright.Editor.Tools.Builtins;
using NUnit.Framework;
using UnityEditor;

namespace KitWright.Editor.Tests
{
    public sealed class PrefsFunctionsTests
    {
        [Test]
        public void ResolvePrefType_KnownTypes()
        {
            Assert.AreEqual("int", PrefsFunctions.ResolvePrefType("int"));
            Assert.AreEqual("float", PrefsFunctions.ResolvePrefType("float"));
            Assert.AreEqual("bool", PrefsFunctions.ResolvePrefType("bool"));
            Assert.AreEqual("string", PrefsFunctions.ResolvePrefType("string"));
        }

        [Test]
        public void ResolvePrefType_CaseInsensitive()
        {
            Assert.AreEqual("int", PrefsFunctions.ResolvePrefType("INT"));
        }

        [Test]
        public void ResolvePrefType_UnknownReturnsAuto()
        {
            Assert.AreEqual("auto", PrefsFunctions.ResolvePrefType("wobble"));
            Assert.AreEqual("auto", PrefsFunctions.ResolvePrefType(null));
        }

        [Test]
        public void TryWritePref_IntRejectsNonInt()
        {
            Assert.IsFalse(PrefsFunctions.TryWritePref("k", "notint", "int", isEditor: true, out var error));
            Assert.AreEqual("not an int", error);
        }

        [Test]
        public void TryWritePref_FloatRejectsNonFloat()
        {
            Assert.IsFalse(PrefsFunctions.TryWritePref("k", "xx", "float", isEditor: true, out _));
        }

        [Test]
        public void TryWritePref_EditorRoundTrip()
        {
            const string key = "gw_test_pref_int";
            EditorPrefs.DeleteKey(key);
            Assert.IsTrue(PrefsFunctions.TryWritePref(key, "42", "int", isEditor: true, out _));
            Assert.AreEqual(42, EditorPrefs.GetInt(key));
            EditorPrefs.DeleteKey(key);
        }

        [Test]
        public void TryWritePref_AutoWritesAsString()
        {
            const string key = "gw_test_pref_auto";
            EditorPrefs.DeleteKey(key);
            Assert.IsTrue(PrefsFunctions.TryWritePref(key, "hello", "auto", isEditor: true, out _));
            Assert.AreEqual("hello", EditorPrefs.GetString(key));
            EditorPrefs.DeleteKey(key);
        }
    }
}
