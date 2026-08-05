// Copyright (C) GameWright. Licensed under MIT.

using GameWright.Editor.Tools.Builtins;
using NUnit.Framework;
#if GAMEWRIGHT_URP
using UnityEngine.Rendering.Universal;
#endif

namespace GameWright.Editor.Tests
{
    public sealed class VolumeFunctionsTests
    {
#if GAMEWRIGHT_URP
        [Test]
        public void ResolveOverrideType_KnownEffectsCaseInsensitive()
        {
            Assert.AreEqual(typeof(Bloom), VolumeFunctions.ResolveOverrideType("bloom"));
            Assert.AreEqual(typeof(Tonemapping), VolumeFunctions.ResolveOverrideType("Tonemapping"));
            Assert.AreEqual(typeof(Vignette), VolumeFunctions.ResolveOverrideType("VIGNETTE"));
        }

        [Test]
        public void ResolveOverrideType_UnknownReturnsNull()
        {
            Assert.IsNull(VolumeFunctions.ResolveOverrideType("NotAnEffect_ZZZ"));
        }

        [Test]
        public void ResolveOverrideType_EmptyReturnsNull()
        {
            Assert.IsNull(VolumeFunctions.ResolveOverrideType(""));
            Assert.IsNull(VolumeFunctions.ResolveOverrideType(null));
        }
#else
        [Test]
        public void WithoutUrp_ReturnsUrpRequiredError()
        {
            dynamic r = VolumeFunctions.CreateVolume();
            Assert.AreEqual("URP_REQUIRED", (string)r.code);
        }
#endif
    }
}
