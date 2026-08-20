// Copyright (C) KitWright. Licensed under MIT.

using System.IO;
using KitWright.Editor.Tools.Builtins;
using NUnit.Framework;
using UnityEngine;

namespace KitWright.Editor.Tests
{
    public sealed class CodeFunctionsTests
    {
        // Under Temp/ rather than Assets/: a .cs dropped into Assets/ triggers a compile and
        // domain reload, which kills the test run this assertion is in.
        private const string Folder = "Temp/__KitWrightCodeFunctionsTests";

        [SetUp]
        public void SetUp()
        {
            Directory.CreateDirectory(FullPath(string.Empty));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(FullPath(string.Empty)))
                Directory.Delete(FullPath(string.Empty), true);
        }

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
        public void CreateScript_RefusesAnExistingFileAndLeavesItUntouched()
        {
            var original = "public class Probe { }";
            File.WriteAllText(FullPath("Probe.cs"), original);

            var refused = CodeFunctions.CreateScript("Probe", "public class Probe { void Clobbered() { } }", Folder);

            StringAssert.Contains("SCRIPT_EXISTS", refused);
            StringAssert.Contains("edit_script", refused);
            StringAssert.Contains("patch_script", refused);
            Assert.AreEqual(original, File.ReadAllText(FullPath("Probe.cs")),
                "A refused create must leave the file untouched.");
        }

        [Test]
        public void EditScript_RejectsAnIntroducedNonBraceError_ButNotAPreexistingOne()
        {
            // Braces balance in all three versions, so a brace count alone cannot tell them apart.
            var sound = "public class Probe { void A() { B(1); } }";
            var broken = "public class Probe { void A() { B(1; } }";

            var soundPath = Folder + "/Sound.txt";
            File.WriteAllText(FullPath("Sound.txt"), sound);
            var rejected = CodeFunctions.EditScript(soundPath, broken, CodeFunctions.ComputeSha256(sound));
            StringAssert.Contains("SYNTAX_REGRESSION", rejected);
            Assert.AreEqual(sound, File.ReadAllText(FullPath("Sound.txt")));

            var brokenPath = Folder + "/Broken.txt";
            File.WriteAllText(FullPath("Broken.txt"), broken);
            var stillBroken = "public class Probe { void A() { B(2; } }";
            var applied = CodeFunctions.EditScript(brokenPath, stillBroken, CodeFunctions.ComputeSha256(broken));
            StringAssert.Contains("Updated script", applied);
            Assert.AreEqual(stillBroken, File.ReadAllText(FullPath("Broken.txt")));
        }

        private static string FullPath(string fileName)
        {
            var root = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
            return Path.Combine(root, Folder, fileName);
        }
    }
}
