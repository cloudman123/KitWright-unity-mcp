// Copyright (C) KitWright. Licensed under MIT.

using KitWright.Editor.Tools.Builtins;
using KitWright.Editor.Tools.Helpers;
using NUnit.Framework;

namespace KitWright.Editor.Tests
{
    public sealed class ComponentSerializerTests
    {
        [Test]
        public void NameFilter_SplitsOnCommasAndMatchesCaseInsensitiveSubstrings()
        {
            Assert.IsEmpty(ComponentPropertyFunctions.SplitFilterTerms(null));
            Assert.IsEmpty(ComponentPropertyFunctions.SplitFilterTerms("  "));
            Assert.IsEmpty(ComponentPropertyFunctions.SplitFilterTerms(" , ,"));

            var terms = ComponentPropertyFunctions.SplitFilterTerms(" resolution , matchWidth ");
            Assert.AreEqual(new[] { "resolution", "matchWidth" }, terms);

            // Serialized names are m_-prefixed and PascalCase; a lowercase substring must still hit.
            Assert.IsTrue(ComponentPropertyFunctions.MatchesAnyTerm("m_ReferenceResolution", terms));
            Assert.IsTrue(ComponentPropertyFunctions.MatchesAnyTerm("m_MatchWidthOrHeight", terms));
            Assert.IsFalse(ComponentPropertyFunctions.MatchesAnyTerm("m_ScaleFactor", terms));
            Assert.IsFalse(ComponentPropertyFunctions.MatchesAnyTerm(null, terms));
        }

        [Test]
        public void ExtractPPtrTypeName_ParsesComponentType()
        {
            Assert.AreEqual("Rigidbody", ComponentSerializer.ExtractPPtrTypeName("PPtr<$Rigidbody>"));
            Assert.AreEqual("GameObject", ComponentSerializer.ExtractPPtrTypeName("PPtr<$GameObject>"));
        }

        [Test]
        public void ExtractPPtrTypeName_NonPPtrReturnsNull()
        {
            Assert.IsNull(ComponentSerializer.ExtractPPtrTypeName("int"));
            Assert.IsNull(ComponentSerializer.ExtractPPtrTypeName(""));
            Assert.IsNull(ComponentSerializer.ExtractPPtrTypeName(null));
        }
    }
}
