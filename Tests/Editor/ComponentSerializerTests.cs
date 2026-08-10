// Copyright (C) KitWright. Licensed under MIT.

using KitWright.Editor.Tools.Helpers;
using NUnit.Framework;

namespace KitWright.Editor.Tests
{
    public sealed class ComponentSerializerTests
    {
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
