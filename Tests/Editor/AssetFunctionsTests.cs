// Copyright (C) KitWright. Licensed under MIT.

using System;
using System.Collections.Generic;
using System.Linq;
using KitWright.Editor.Tools.Builtins;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KitWright.Editor.Tests
{
    public sealed class AssetFunctionsTests
    {
        private const string FolderName = "__KitWrightAssetFunctionsTests";
        private string _folder;

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/" + FolderName;
            if (!AssetDatabase.IsValidFolder(_folder))
                AssetDatabase.CreateFolder("Assets", FolderName);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(_folder))
                AssetDatabase.DeleteAsset(_folder);
        }

        [Test]
        public void DeleteAsset_MovesTheAssetToTheTrashInsteadOfUnlinkingIt()
        {
            var path = _folder + "/Doomed_" + Guid.NewGuid().ToString("N") + ".mat";
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            Assert.IsNotNull(shader, "A test shader is required.");
            AssetDatabase.CreateAsset(new Material(shader), path);
            AssetDatabase.SaveAssets();
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(path), "Setup failed: asset was not created.");

            var result = AssetFunctions.DeleteAsset(path);

            StringAssert.Contains("trash", result);
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Material>(path), "Asset is still loadable after delete.");
        }

        [Test]
        public void DeleteSpriteAtlas_MovesTheAtlasToTheTrashInsteadOfUnlinkingIt()
        {
            var path = _folder + "/Doomed_" + Guid.NewGuid().ToString("N") + ".spriteatlas";
            var created = SpriteAtlasFunctions.CreateSpriteAtlas(path).ToString();
            Assert.IsNotNull(AssetDatabase.LoadMainAssetAtPath(path), "Setup failed: " + created);

            var result = SpriteAtlasFunctions.DeleteSpriteAtlas(path).ToString();

            StringAssert.Contains("trash", result);
            Assert.IsNull(AssetDatabase.LoadMainAssetAtPath(path), "Atlas is still loadable after delete.");
        }

        [Test]
        public void DeleteAsset_MissingPathReportsAssetNotFound()
        {
            var result = AssetFunctions.DeleteAsset(_folder + "/NoSuchAsset.mat");

            StringAssert.Contains("ASSET_NOT_FOUND", result);
        }

        [Test]
        public void FindAssets_PagesJoinIntoTheSameListAsOneWholeRead()
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            Assert.IsNotNull(shader, "A test shader is required.");
            var tag = "KitWrightPaged" + Guid.NewGuid().ToString("N").Substring(0, 8);
            for (var i = 0; i < 3; i++)
                AssetDatabase.CreateAsset(new Material(shader), $"{_folder}/{tag}_{i}.mat");
            AssetDatabase.SaveAssets();

            var filter = "t:Material " + tag;
            var whole = PathsIn(AssetFunctions.FindAssets(filter, max: 50));
            Assert.AreEqual(3, whole.Count, "Setup failed: the three materials are not findable.");

            var first = AssetFunctions.FindAssets(filter, max: 2);
            StringAssert.Contains("Found 3 assets.", first);
            StringAssert.Contains("Showing 1-2 of 3; pass cursor=2", first);

            var second = AssetFunctions.FindAssets(filter, max: 2, cursor: 2);
            StringAssert.Contains("end of the list", second);

            var walked = PathsIn(first).Concat(PathsIn(second)).ToList();
            CollectionAssert.AreEqual(whole, walked,
                "Two pages must reproduce the whole list exactly - no gap, no repeat.");

            // Pins the contract only: the API happens to hand back sorted paths here, so removing
            // the OrderBy does not currently fail anything.
            CollectionAssert.AreEqual(whole.OrderBy(p => p, StringComparer.Ordinal).ToList(), whole,
                "find_assets must return paths in a stable order for the cursor to mean anything.");
        }

        [Test]
        public void FindAssets_CursorPastTheEndSaysSoInsteadOfReturningPageOne()
        {
            // A borrowed filter made this depend on what the editor version ships: an empty result
            // answers "no assets found" and never reaches the cursor check.
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            Assert.IsNotNull(shader, "A test shader is required.");
            var tag = "KitWrightPastEnd" + Guid.NewGuid().ToString("N").Substring(0, 8);
            AssetDatabase.CreateAsset(new Material(shader), $"{_folder}/{tag}.mat");
            AssetDatabase.SaveAssets();

            var result = AssetFunctions.FindAssets("t:Material " + tag, max: 1, cursor: 100000);

            StringAssert.Contains("past the end", result);
            Assert.IsFalse(result.Contains("  - "), "A past-the-end cursor must list nothing.");
        }

        private static List<string> PathsIn(string response) =>
            response.Split('\n')
                .Where(line => line.TrimStart().StartsWith("- "))
                .Select(line => line.Trim().Substring(2))
                .ToList();
    }
}
