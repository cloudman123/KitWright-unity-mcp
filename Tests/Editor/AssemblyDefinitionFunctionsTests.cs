// Copyright (C) GameWright. Licensed under MIT.

using System.Linq;
using GameWright.Editor.Tools.Builtins;
using NUnit.Framework;

namespace GameWright.Editor.Tests
{
    public sealed class AssemblyDefinitionFunctionsTests
    {
        [Test]
        public void SplitCsv_EmptyReturnsEmpty()
        {
            Assert.IsEmpty(AssemblyDefinitionFunctions.SplitCsv(null));
            Assert.IsEmpty(AssemblyDefinitionFunctions.SplitCsv("   "));
        }

        [Test]
        public void SplitCsv_TrimsAndDropsBlanks()
        {
            var parts = AssemblyDefinitionFunctions.SplitCsv(" A , B ,, C ");
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, parts);
        }

        [Test]
        public void ResolveReferences_GuidTokenPassesThrough()
        {
            var result = AssemblyDefinitionFunctions.ResolveReferences(new[] { "GUID:abc123" }).ToList();
            CollectionAssert.AreEqual(new[] { "GUID:abc123" }, result);
        }

        [Test]
        public void ResolveReferences_UnknownAssemblyKeptAsPlainName()
        {
            var result = AssemblyDefinitionFunctions.ResolveReferences(new[] { "Definitely.Not.An.Assembly" }).ToList();
            CollectionAssert.AreEqual(new[] { "Definitely.Not.An.Assembly" }, result);
        }
    }
}
